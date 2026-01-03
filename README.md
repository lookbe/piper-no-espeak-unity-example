# piper-unity

A Fast, Local Neural Text-to-Speech System: Piper in Unity for Multi-Platform.

## Overview

**piper-unity** is a high-performance, on-device text-to-speech (TTS) integration for Unity. This version is a specialized fork/port of the [Piper](https://github.com/rhasspy/piper) project, optimized for real-time applications and game development.

### ⚖️ Why this version?
Unlike the original Piper implementation which relies on `espeak-ng` (GPL Licensed), this repository has been rewritten to be **commercial-friendly** and **performant**:
- **Permissive Licensing**: Removed all GPL-licensed components. 
- **Open Phonemizer**: Replaced `espeak-ng` with a permissive-license phonemizer backend.
- **ONNX Runtime**: Replaced Unity Sentis with [onnxruntime-unity](https://github.com/asus4/onnxruntime-unity) for faster inference speeds and superior platform stability.

---

## Features

* ✅ **Permissive Stack**: No GPL dependencies—suitable for commercial Unity projects.
* ✅ **High Performance**: Real-time synthesis powered by ONNX Runtime.
* ✅ **Multi-platform**: Native support for Windows, macOS, and Android.
* ✅ **Fully Offline**: All processing happens on-device; no internet connection required.
* ✅ **Lightweight**: Optimized neural models perfect for mobile deployment.

---

## Language Support

> [!IMPORTANT]  
> **Current Version Support:** The current implementation of the Open Phonemizer backend supports **English only**. 
> 
> While the Piper neural engine is capable of many languages, the phoneme conversion layer in this repository is currently optimized for English (`en-us` / `en-gb`). Support for additional languages is planned for future updates as more permissive phoneme dictionaries are integrated.

---

## Requirements

* **Unity**: `6000.0.58f2` (Unity 6) or higher.
* **Inference Engine**: [onnxruntime-unity](https://github.com/asus4/onnxruntime-unity) (v2.2.1+).
* **Phonemizer Resources**: Open Phonemizer ONNX weights and dictionaries.

---

## Architecture

### 1. Open Phonemizer
The text-to-phoneme conversion is handled by a permissive-license implementation. By utilizing a dedicated ONNX-based tokenizer and phoneme dictionary, we eliminate the legal complexities of `espeak-ng` while maintaining high accuracy for Piper's neural models.

### 2. ONNX Runtime Inference
By using **ONNX Runtime**, this integration provides a highly optimized C++ backend for each platform, ensuring that voice synthesis does not bottleneck the Unity main thread.

---

## Getting Started

### 1. Installation
1.  **Clone the repository** into your Unity `Assets` folder.
2.  **Install ONNX Runtime**: Follow the installation guide for [onnxruntime-unity](https://github.com/asus4/onnxruntime-unity).

### 2. Required Model Assets
To run the phonemizer and TTS, you must download the following assets from [lookbe/open-phonemizer-onnx](https://huggingface.co/lookbe/open-phonemizer-onnx/tree/main) and place them in your `Assets/StreamingAssets` folder:

* `model.onnx`
* `tokenizer.json`
* `phoneme_dict.json`

*Note: Also ensure you have an English Piper voice model (`.onnx` and `.json`) in the same directory.*

### 3. Run the Demo
1.  Open the scene located at `Assets/Scenes/PiperScene.unity`.
2.  Press **Play**.
3.  Enter text in the UI and click **Speak** to trigger local synthesis.

---

## Platform Support

| Platform | Status | Runtime Backend | License |
| :--- | :--- | :--- | :--- |
| **Windows** | ✅ | ONNX (DirectML/CPU) | Permissive |
| **macOS** | ✅ | ONNX (CoreML/CPU) | Permissive |
| **Android** | ✅ | ONNX (NNAPI/CPU) | Permissive |

---

## Demo Video

[![Piper Unity](https://img.youtube.com/vi/i2LvqWICb40/0.jpg)](https://www.youtube.com/watch?v=i2LvqWICb40)

---

## Links
* [Open Phonemizer ONNX Models (Hugging Face)](https://huggingface.co/lookbe/open-phonemizer-onnx)
* [Piper Official](https://github.com/rhasspy/piper)
* [onnxruntime-unity Repository](https://github.com/asus4/onnxruntime-unity)

## License
The integration code and phonemizer logic are provided under permissive licenses (MIT/Apache 2.0). Individual voice models and phonemizer weights are subject to the licenses provided on their respective repositories.
