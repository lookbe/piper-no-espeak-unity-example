using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine.Networking;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenPhonemizer; 

[RequireComponent(typeof(AudioSource))]
public class PiperManager : MonoBehaviour
{
    [Header("Piper Model Settings")]
    public string modelFileName = "model.onnx";
    public ESpeakTokenizer tokenizer; // Maps phonemes to IDs for Piper

    [Header("OpenPhonemizer Settings")]
    public string phonemizerModelName = "phonemizer_model.onnx";
    public string phonemizerConfigName = "tokenizer.json";
    public string phonemizerDictName = "phoneme_dict.json";

    public Text voiceNameText;

    private InferenceSession session;
    private OpenPhonemizer.Phonemizer g2p; 
    private AudioSource audioSource;
    private bool isInitialized = false;

    private bool hasSidKey = false;
    private int speakerId = 0;


    [Range(0.0f, 1.0f)] public float commaDelay = 0.1f;
    [Range(0.0f, 1.0f)] public float periodDelay = 0.5f;
    [Range(0.0f, 1.0f)] public float questionExclamationDelay = 0.6f;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            Debug.LogError("AudioSource component not found! It will be added automatically.");
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        StartCoroutine(InitializePiper());
    }

    private IEnumerator InitializePiper()
    {
        string persistentDataPath = Application.persistentDataPath;
        string streamingAssetsPath = Application.streamingAssetsPath;

        // Paths for Piper Model
        string piperModelPathId = Path.Combine(persistentDataPath, modelFileName);
        
        // Paths for Phonemizer
        string phModelPath = Path.Combine(persistentDataPath, phonemizerModelName);
        string phConfigPath = Path.Combine(persistentDataPath, phonemizerConfigName);
        string phDictPath = Path.Combine(persistentDataPath, phonemizerDictName);

        #if UNITY_ANDROID && !UNITY_EDITOR
            // Android: Copy all required files from StreamingAssets to persistentDataPath
            yield return StartCoroutine(CopyFile(modelFileName, piperModelPathId));
            yield return StartCoroutine(CopyFile(phonemizerModelName, phModelPath));
            yield return StartCoroutine(CopyFile(phonemizerConfigName, phConfigPath));
            yield return StartCoroutine(CopyFile(phonemizerDictName, phDictPath));
        #else
            // Editor: Use StreamingAssets directly
            piperModelPathId = Path.Combine(streamingAssetsPath, modelFileName);
            phModelPath = Path.Combine(streamingAssetsPath, phonemizerModelName);
            phConfigPath = Path.Combine(streamingAssetsPath, phonemizerConfigName);
            phDictPath = Path.Combine(streamingAssetsPath, phonemizerDictName);
            yield return null;
        #endif

        // 1. Initialize OpenPhonemizer
        try 
        {
            Debug.Log($"Initializing OpenPhonemizer with: {phModelPath}");
            g2p = new OpenPhonemizer.Phonemizer(phModelPath, phDictPath, phConfigPath);
            Debug.Log("OpenPhonemizer initialized.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize OpenPhonemizer: {e.Message}");
            yield break;
        }

        // 2. Initialize Piper Model
        try
        {
            Debug.Log($"Creating InferenceSession for Piper with model at: {piperModelPathId}");
            session = new InferenceSession(piperModelPathId);
            
            // Auto-detect if "sid" input exists
            if (session.InputMetadata.ContainsKey("sid"))
            {
                hasSidKey = true;
                Debug.Log("Piper Model requires speaker ID (sid).");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to perform Piper inference session initialization: {e.Message}");
            yield break;
        }

        if (voiceNameText != null)
             voiceNameText.text = $"Model: {modelFileName}";

        Debug.Log("Piper Manager initialized.");
        isInitialized = true;

        _WarmupModel();
        Debug.Log("Finished warmup.");
    }

    private IEnumerator CopyFile(string fileName, string destPath)
    {
        if (!File.Exists(destPath))
        {
            Debug.Log($"Copying {fileName} to {destPath}...");
            string sourcePath = Path.Combine(Application.streamingAssetsPath, fileName);
            using (UnityWebRequest www = UnityWebRequest.Get(sourcePath))
            {
                yield return www.SendWebRequest();
                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Failed to copy {fileName}: {www.error}");
                }
                else
                {
                    File.WriteAllBytes(destPath, www.downloadHandler.data);
                    Debug.Log($"Successfully copied {fileName}.");
                }
            }
        }
        else
        {
            Debug.Log($"File {fileName} already exists at destination.");
        }
    }

    void OnDestroy()
    {
        session?.Dispose();
        session = null;
        g2p?.Dispose();
        g2p = null;
    }

    public void OnSubmitText(Text textField)
    {
        if (string.IsNullOrEmpty(textField.text))
        {
            Debug.LogError("Input text is empty. Please enter some text.");
            return;
        }

        Debug.Log($"Input text: {textField.text}");
        SynthesizeAndPlay(textField.text);
    }

    public void SynthesizeAndPlay(string text)
    {
        if (!isInitialized)
        {
            Debug.LogError("Piper Manager is not initialized.");
            return;
        }
        StartCoroutine(SynthesizeAndPlayCoroutine(text));
    }

    private IEnumerator SynthesizeAndPlayCoroutine(string text)
    {
        string delayPunctuationPattern = @"([,.?!;:])";
        string nonDelayPunctuationPattern = @"[^\w\s,.?!;:]";

        string[] parts = Regex.Split(text, delayPunctuationPattern);

        foreach (string part in parts)
        {
            if (string.IsNullOrEmpty(part.Trim()))
            {
                continue;
            }

            bool isDelayPunctuation = Regex.IsMatch(part, "^" + delayPunctuationPattern + "$");

            if (isDelayPunctuation)
            {
                float delay = 0f;
                switch (part)
                {
                    case ",":
                    case ";":
                    case ":":
                        delay = commaDelay;
                        break;
                    case ".":
                        delay = periodDelay;
                        break;
                    case "?":
                    case "!":
                        delay = questionExclamationDelay;
                        break;
                }
                if (delay > 0)
                {
                    Debug.Log($"Pausing for '{part}' for {delay} seconds.");
                    yield return new WaitForSeconds(delay);
                }
            }
            else
            {
                string cleanedChunk = Regex.Replace(part, nonDelayPunctuationPattern, " ");
                cleanedChunk = cleanedChunk.Trim();

                if (!string.IsNullOrEmpty(cleanedChunk))
                {
                    Debug.Log($"Processing text chunk: \"{cleanedChunk}\"");
                    _SynthesizeAndPlayChunk(cleanedChunk);
                    yield return new WaitWhile(() => audioSource.isPlaying);
                }
            }
        }
        Debug.Log("Finished playing all chunks.");
    }

    private void _SynthesizeAndPlayChunk(string textChunk)
    {
        string phonemeStr = Phonemize(textChunk);
        if (string.IsNullOrEmpty(phonemeStr))
        {
            Debug.LogError($"Phoneme conversion failed for chunk: \"{textChunk}\"");
            return;
        }

        // The OpenPhonemizer returns phonemes. We need to split them into individual characters for the tokenizer
        // e.g. "h@loU" -> ["h", "@", "l", "o", "U"]
        string[] phonemeArray = phonemeStr.ToCharArray().Select(c => c.ToString()).ToArray();
        
        // Piper onnx models typically expect Int64 (long) inputs
        int[] phonemeTokensInt = tokenizer.Tokenize(phonemeArray);
        long[] phonemeTokens = phonemeTokensInt.Select(x => (long)x).ToArray();

        float[] scales = tokenizer.GetInferenceParams();
        long[] inputLength = { phonemeTokens.Length };

        Debug.Log($"Model inputs prepared. Token count: {inputLength[0]}, Scales: [{string.Join(", ", scales)}]");

        try 
        {
            var inputs = new List<NamedOnnxValue>();

            // 1. input (phoneme IDs)
            var inputTensor = new DenseTensor<long>(phonemeTokens, new[] { 1, phonemeTokens.Length });
            inputs.Add(NamedOnnxValue.CreateFromTensor("input", inputTensor));

            // 2. input_lengths
            var lengthTensor = new DenseTensor<long>(inputLength, new[] { 1 });
            inputs.Add(NamedOnnxValue.CreateFromTensor("input_lengths", lengthTensor));

            // 3. scales
            var scalesTensor = new DenseTensor<float>(scales, new[] { 3 });
            inputs.Add(NamedOnnxValue.CreateFromTensor("scales", scalesTensor));

            // 4. sid (if needed)
            if (hasSidKey)
            {
                var sidTensor = new DenseTensor<long>(new long[] { speakerId }, new[] { 1 });
                inputs.Add(NamedOnnxValue.CreateFromTensor("sid", sidTensor)); 
            }

            using (var results = session.Run(inputs))
            {
                // Piper output is usually named "output"
                var outputResult = results.FirstOrDefault(); 
                if (outputResult == null)
                {
                    Debug.LogError("Model returned no results.");
                    return;
                }

                var outputTensor = outputResult.AsTensor<float>();
                float[] audioData = outputTensor.ToArray();

                if (audioData == null || audioData.Length == 0)
                {
                    Debug.LogError("Failed to generate audio data or the data is empty.");
                    return;
                }
                Debug.Log($"Generated audio data length: {audioData.Length}");

                int sampleRate = tokenizer.SampleRate;
                
                AudioClip clip = AudioClip.Create("GeneratedSpeech", audioData.Length, 1, sampleRate, false);
                clip.SetData(audioData, 0);

                Debug.Log($"Speech generated! AudioClip length: {clip.length:F2}s. Playing.");
                audioSource.PlayOneShot(clip);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Inference failed: {e.Message}");
        }
    }

    private void _WarmupModel()
    {
        Debug.Log("Warming up the model with a dummy run...");
        string warmupText = "h"; 

        string phonemeStr = Phonemize(warmupText);
        if (string.IsNullOrEmpty(phonemeStr))
        {
            Debug.LogError("Warmup failed: Phoneme conversion failed.");
            return;
        }

        string[] phonemeArray = phonemeStr.ToCharArray().Select(c => c.ToString()).ToArray();
        int[] phonemeTokensInt = tokenizer.Tokenize(phonemeArray);
        long[] phonemeTokens = phonemeTokensInt.Select(x => (long)x).ToArray();
        
        float[] scales = tokenizer.GetInferenceParams();
        long[] inputLength = { phonemeTokens.Length };

        try
        {
            var inputs = new List<NamedOnnxValue>();
            inputs.Add(NamedOnnxValue.CreateFromTensor("input", new DenseTensor<long>(phonemeTokens, new[] { 1, phonemeTokens.Length })));
            inputs.Add(NamedOnnxValue.CreateFromTensor("input_lengths", new DenseTensor<long>(inputLength, new[] { 1 })));
            inputs.Add(NamedOnnxValue.CreateFromTensor("scales", new DenseTensor<float>(scales, new[] { 3 })));
            
            if (hasSidKey)
            {
                 inputs.Add(NamedOnnxValue.CreateFromTensor("sid", new DenseTensor<long>(new long[] { 0 }, new[] { 1 })));
            }

            using (var results = session.Run(inputs))
            {
                var outputResult = results.FirstOrDefault();
                if (outputResult != null)
                {
                     Debug.Log("Model warmup successful.");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Warmup failed: {e.Message}");
        }
    }
    
    private string Phonemize(string text)
    {
        if (g2p == null)
        {
            Debug.LogError("OpenPhonemizer is not initialized.");
            return null;
        }

        Debug.Log($"Phonemizing text: \"{text}\"");
        try
        {
            string phonemes = g2p.Phonemize(text);
            Debug.Log($"[PHONEMES] {phonemes}");
            return phonemes;
        }
        catch (Exception e)
        {
            Debug.LogError($"Phonemization failed: {e.Message}");
            return null;
        }
    }
}
