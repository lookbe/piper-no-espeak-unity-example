using System.Collections;
using System.IO;
using UnityEngine;

public class InitModel : MonoBehaviour
{
    public PiperTTS.PiperTTS tts;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    IEnumerator Start()
    {
        yield return new WaitUntil(() => !string.IsNullOrEmpty(AndroidObbMount.AndroidObbMount.mountPoint));

        tts.piperModelPath = GetAbsolutePath(tts.piperModelPath);
        tts.piperConfigPath = GetAbsolutePath(tts.piperConfigPath);

        tts.phonemizerModelPath = GetAbsolutePath(tts.phonemizerModelPath);
        tts.phonemizerConfigPath = GetAbsolutePath(tts.phonemizerConfigPath);
        tts.phonemizerDictPath = GetAbsolutePath(tts.phonemizerDictPath);

        tts.InitModel();
    }

    string GetAbsolutePath(string filepath)
    {
        return Path.IsPathRooted(filepath) ? filepath : Path.Join(AndroidObbMount.AndroidObbMount.mountPoint, filepath);
    }
}
