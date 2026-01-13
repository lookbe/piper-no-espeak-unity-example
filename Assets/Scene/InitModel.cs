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

        tts.piperModelPath = Path.IsPathRooted(tts.piperModelPath) ? tts.piperModelPath : Path.Join(AndroidObbMount.AndroidObbMount.mountPoint, tts.piperModelPath);
        tts.piperConfigPath = Path.IsPathRooted(tts.piperConfigPath) ? tts.piperConfigPath : Path.Join(AndroidObbMount.AndroidObbMount.mountPoint, tts.piperConfigPath);

        tts.phonemizerModelPath = Path.IsPathRooted(tts.phonemizerModelPath) ? tts.phonemizerModelPath : Path.Join(AndroidObbMount.AndroidObbMount.mountPoint, tts.phonemizerModelPath);
        tts.phonemizerConfigPath = Path.IsPathRooted(tts.phonemizerConfigPath) ? tts.piperModelPath : Path.Join(AndroidObbMount.AndroidObbMount.mountPoint, tts.phonemizerConfigPath);
        tts.phonemizerDictPath = Path.IsPathRooted(tts.phonemizerDictPath) ? tts.piperModelPath : Path.Join(AndroidObbMount.AndroidObbMount.mountPoint, tts.phonemizerDictPath);

        tts.InitModel();
    }
}
