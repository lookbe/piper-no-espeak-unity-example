using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using UnityEngine;

namespace OpenPhonemizer
{

    // config classes
    public class TokenizerConfig
    {
        [JsonProperty("text_symbols")]
        public Dictionary<string, int> TextSymbols { get; set; }

        [JsonProperty("phoneme_symbols")]
        public Dictionary<string, string> PhonemeSymbols { get; set; }

        [JsonProperty("char_repeats")]
        public int CharRepeats { get; set; }
        
        [JsonProperty("languages")]
        public List<string> Languages { get; set; }
    }

    public class Tokenizer
    {
        private readonly TokenizerConfig _config;

        public Tokenizer(TokenizerConfig config)
        {
            _config = config;
        }

        public long[] Tokenize(string text, string lang = "en_us")
        {
            var charRepeats = _config.CharRepeats;
            var textSymbols = _config.TextSymbols;
            
            // Basic lowercasing
            var chars = text.ToLowerInvariant().ToCharArray();
            var sequence = new List<long>();

            // Add start token: <lang>
            string startToken = $"<{lang}>";
            if (textSymbols.TryGetValue(startToken, out int startIndex))
            {
                sequence.Add(startIndex);
            }

            foreach (var c in chars)
            {
                string s = c.ToString();
                if (textSymbols.TryGetValue(s, out int idx))
                {
                    for (int i = 0; i < charRepeats; i++)
                    {
                        sequence.Add(idx);
                    }
                }
            }

            // Add end token: <end>
            if (textSymbols.TryGetValue("<end>", out int endIndex))
            {
                sequence.Add(endIndex);
            }

            return sequence.ToArray();
        }

        public string Decode(long[] tokens)
        {
             // Simple decode
             var phonemes = new List<string>();
             foreach(var t in tokens)
             {
                 string sKey = t.ToString();
                 if(_config.PhonemeSymbols.TryGetValue(sKey, out string ph))
                 {
                     // Filter specials
                     if(!ph.StartsWith("<") && ph != "_")
                     {
                         phonemes.Add(ph);
                     }
                 }
             }
             return string.Join("", phonemes);
        }
    }

    public class Phonemizer : IDisposable
    {
        private InferenceSession _session;
        private Tokenizer _tokenizer;
        private Dictionary<string, Dictionary<string, string>> _dict;

        public Phonemizer(string modelPath, string dictPath, string tokenizerConfigPath)
        {
            try
            {
                _session = new InferenceSession(modelPath);
                Debug.Log($"[OpenPhonemizer] Loaded model from {modelPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[OpenPhonemizer] Failed to load model: {e.Message}");
                throw;
            }

            // Load Tokenizer Config
            try 
            {
                string jsonString = File.ReadAllText(tokenizerConfigPath);
                var config = JsonConvert.DeserializeObject<TokenizerConfig>(jsonString);
                _tokenizer = new Tokenizer(config);
                Debug.Log($"[OpenPhonemizer] Loaded tokenizer config from {tokenizerConfigPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[OpenPhonemizer] Failed to load tokenizer config: {e.Message}");
                throw;
            }

            // Load Dictionary
            try
            {
                if (File.Exists(dictPath))
                {
                    string dictJson = File.ReadAllText(dictPath);
                    _dict = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(dictJson);
                    Debug.Log($"[OpenPhonemizer] Loaded dictionary from {dictPath}");
                }
                else
                {
                    Debug.LogWarning($"[OpenPhonemizer] Dictionary file not found at {dictPath}. Proceeding without dictionary.");
                    _dict = new Dictionary<string, Dictionary<string, string>>();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[OpenPhonemizer] Failed to load dictionary: {e.Message}");
                _dict = new Dictionary<string, Dictionary<string, string>>();
            }
        }


        public string Phonemize(string text, string lang = "en_us")
        {
            if (string.IsNullOrEmpty(text)) return "";

            // Simple split by punctuation
            string pattern = @"([().,:?!/–\s])"; // Punctuation + whitespace
            string[] parts = Regex.Split(text, pattern);
            
            var resultParts = new List<string>();
            var punctSet = new HashSet<string>() { "(", ")", ".", ",", ":", "?", "!", "/", "–", " ", "-" };

            // Check dictionary availability for lang
            Dictionary<string, string> langDict = null;
            if (_dict != null && _dict.ContainsKey(lang))
            {
                langDict = _dict[lang];
            }

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                if (punctSet.Contains(part))
                {
                    resultParts.Add(part);
                    continue;
                }
                
                // Dictionary Lookup
                bool found = false;
                if (langDict != null)
                {
                    if (langDict.TryGetValue(part, out string ph)) { resultParts.Add(ph); found = true; }
                    else if (langDict.TryGetValue(part.ToLowerInvariant(), out ph)) { resultParts.Add(ph); found = true; }
                }

                if (found) continue;

                // Model Inference
                try
                {
                    long[] inputTokens = _tokenizer.Tokenize(part, lang);
                    
                    var inputTensor = new DenseTensor<long>(inputTokens, new[] { 1, inputTokens.Length });
                    var inputs = new List<NamedOnnxValue>
                    {
                        NamedOnnxValue.CreateFromTensor("text", inputTensor)
                    };

                    using var results = _session.Run(inputs);
                    var logits = results.First().AsTensor<float>(); 
                    
                    // Argmax & Dedup
                    var decodedTokens = new List<long>();
                    int sequenceLength = logits.Dimensions[1];
                    int vocabSize = logits.Dimensions[2];
                    
                    long lastToken = -1;

                    for(int t = 0; t < sequenceLength; t++)
                    {
                        float maxVal = float.MinValue;
                        int maxIdx = 0;
                        for(int v = 0; v < vocabSize; v++)
                        {
                            float val = logits[0, t, v];
                            if(val > maxVal)
                            {
                                maxVal = val;
                                maxIdx = v;
                            }
                        }

                        if(t == 0 || maxIdx != lastToken) 
                        {
                            decodedTokens.Add(maxIdx);
                            lastToken = maxIdx;
                        }
                    }
                    
                    string phonemes = _tokenizer.Decode(decodedTokens.ToArray());
                    resultParts.Add(phonemes);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[OpenPhonemizer] Inference error for word '{part}': {e.Message}");
                    resultParts.Add(part); // Fallback to original text? or empty?
                }
            }

            return string.Join("", resultParts);
        }

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}
