using System;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine.Networking;
using System.IO.Compression;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

[RequireComponent(typeof(AudioSource))]
public class PiperManager : MonoBehaviour
{
    public string modelFileName = "model.onnx";
    public ESpeakTokenizer tokenizer;

    public Text voiceNameText;

    private InferenceSession session;
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
        string espeakDataPath;
        string modelPath;

        #if UNITY_ANDROID && !UNITY_EDITOR
            espeakDataPath = Path.Combine(Application.persistentDataPath, "espeak-ng-data");
            modelPath = Path.Combine(Application.persistentDataPath, modelFileName);

            // 1. Setup eSpeak Data (copy if needed)
            if (!Directory.Exists(espeakDataPath))
            {
                Debug.Log("Android: eSpeak data not found in persistentDataPath. Starting copy process...");

                string zipSourcePath = Path.Combine(Application.streamingAssetsPath, "espeak-ng-data.zip");
                string zipDestPath = Path.Combine(Application.persistentDataPath, "espeak-ng-data.zip");

                using (UnityWebRequest www = UnityWebRequest.Get(zipSourcePath))
                {
                    yield return www.SendWebRequest();

                    if (www.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"Failed to load espeak-ng-data.zip from StreamingAssets: {www.error}");
                        yield break;
                    }

                    File.WriteAllBytes(zipDestPath, www.downloadHandler.data);

                    try
                    {
                        ZipFile.ExtractToDirectory(zipDestPath, Application.persistentDataPath);
                        Debug.Log("eSpeak data successfully unzipped to persistentDataPath.");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error unzipping eSpeak data: {e.Message}");
                        yield break;
                    }
                    finally
                    {
                        if (File.Exists(zipDestPath))
                        {
                            File.Delete(zipDestPath);
                        }
                    }
                }
            }
            else
            {
                Debug.Log("Android: eSpeak data already exists in persistentDataPath.");
            }

            // 2. Setup Model File (copy if needed)
            if (!File.Exists(modelPath))
            {
                Debug.Log($"Android: Model file '{modelFileName}' not found in persistentDataPath. Copying from StreamingAssets...");
                string modelSourcePath = Path.Combine(Application.streamingAssetsPath, modelFileName);
                
                using (UnityWebRequest www = UnityWebRequest.Get(modelSourcePath))
                {
                    yield return www.SendWebRequest();
                    if (www.result != UnityWebRequest.Result.Success)
                    {
                         Debug.LogError($"Failed to load model file '{modelFileName}' from StreamingAssets: {www.error}");
                         yield break;
                    }
                    File.WriteAllBytes(modelPath, www.downloadHandler.data);
                    Debug.Log($"Model file copied to: {modelPath}");
                }
            }

        #else
            espeakDataPath = Path.Combine(Application.streamingAssetsPath, "espeak-ng-data");
            modelPath = Path.Combine(Application.streamingAssetsPath, modelFileName);
            Debug.Log($"Editor/Standalone: Using eSpeak data directly from StreamingAssets: {espeakDataPath}");
            yield return null;
        #endif

        InitializeESpeak(espeakDataPath);

        try
        {
            Debug.Log($"Creating InferenceSession with model at: {modelPath}");
            session = new InferenceSession(modelPath);
            
            // Auto-detect if "sid" input exists
            if (session.InputMetadata.ContainsKey("sid"))
            {
                hasSidKey = true;
                Debug.Log("Model requires speaker ID (sid).");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to perform inference session initialization: {e.Message}");
            yield break;
        }

        if (voiceNameText != null)
             voiceNameText.text = $"Model: {modelFileName}";

        Debug.Log("Piper Manager initialized.");
        isInitialized = true;

        _WarmupModel();
        Debug.Log("Finished warmup.");
    }

    private void InitializeESpeak(string dataPath)
    {
        int initResult = ESpeakNG.espeak_Initialize(0, 0, dataPath, 0);

        if (initResult > 0)
        {
            Debug.Log($"[PiperManager] eSpeak-ng Initialization SUCCEEDED. Data path: {dataPath}");

            if (tokenizer == null || string.IsNullOrEmpty(tokenizer.Voice))
            {
                Debug.LogError("[PiperManager] Tokenizer is not assigned or has no voice name.");
                return;
            }

            string voiceName = tokenizer.Voice;
            int voiceResult = ESpeakNG.espeak_SetVoiceByName(voiceName);

            if (voiceResult == 0)
                Debug.Log($"[PiperManager] Set voice to '{voiceName}' SUCCEEDED.");
            else
                Debug.LogError($"[PiperManager] Set voice to '{voiceName}' FAILED. Error code: {voiceResult}");
        }
        else
        {
            Debug.LogError($"[PiperManager] eSpeak-ng Initialization FAILED. Error code: {initResult}");
        }
    }

    void OnDestroy()
    {
        session?.Dispose();
        session = null;
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

        string[] phonemeArray = phonemeStr.Trim().Select(c => c.ToString()).ToArray();
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
                // It is a float tensor
                var outputResult = results.FirstOrDefault(); // or results.First(r => r.Name == "output")
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
                // Unity AudioClip expects data in range [-1, 1]. Piper output is usually raw audio samples.
                // Assuming the output is already normalized or close to it. (Piper usually outputs raw float samples).
                
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
        string warmupText = "h"; // Keep it very short

        string phonemeStr = Phonemize(warmupText);
        if (string.IsNullOrEmpty(phonemeStr))
        {
            Debug.LogError("Warmup failed: Phoneme conversion failed.");
            return;
        }

        string[] phonemeArray = phonemeStr.Trim().Select(c => c.ToString()).ToArray();
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
        Debug.Log($"Phonemizing text: \"{text}\"");
        IntPtr textPtr = IntPtr.Zero;
        try
        {
            Debug.Log($"[PiperManager] Cleaned text for phonemization: \"{text}\"");
            byte[] textBytes = Encoding.UTF8.GetBytes(text + "\0");
            textPtr = Marshal.AllocHGlobal(textBytes.Length);
            Marshal.Copy(textBytes, 0, textPtr, textBytes.Length);
            
            IntPtr pointerToText = textPtr;

            int textMode = 0; // espeakCHARS_AUTO=0
            int phonemeMode = 2; // bit 1: 0=eSpeak's ascii phoneme names, 1= International Phonetic Alphabet (as UTF-8 characters).

            IntPtr resultPtr = ESpeakNG.espeak_TextToPhonemes(ref pointerToText, textMode, phonemeMode);

            if (resultPtr != IntPtr.Zero)
            {
                string phonemeString = PtrToUtf8String(resultPtr);
                Debug.Log($"[PHONEMES] {phonemeString}");
                return phonemeString;
            }
            else
            {
                Debug.LogError("[PiperManager] Phonemize FAILED. The function returned a null pointer.");
                return null;
            }
        }
        finally
        {
            if (textPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(textPtr);
            }
        }
    }
    
    private static string PtrToUtf8String(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return "";
        var byteList = new List<byte>();
        for (int offset = 0; ; offset++)
        {
            byte b = Marshal.ReadByte(ptr, offset);
            if (b == 0) break;
            byteList.Add(b);
        }
        return Encoding.UTF8.GetString(byteList.ToArray());
    }
}
