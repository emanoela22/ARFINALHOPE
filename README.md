# ARFINALHOPE

Unity project with butterfly visual-scanning exercises and a camera-based Hand Music mode.

## Opening the project

Use **Unity 6000.0.58f2**. For Android builds, install Android Build Support and its SDK, NDK and OpenJDK modules through Unity Hub.

Before opening a fresh clone, restore the hand-tracking package as described below. Then add the **`FinalProject`** folder to Unity Hub (not the repository root) and open it. Allow Unity to finish importing assets and resolving packages.

## Required download: MediaPipe hand tracking

Hand Music depends on **MediaPipe Unity Plugin v0.16.3**. The package archive is approximately **277 MiB**, so it is deliberately excluded from Git with `.gitignore`. Cloning this repository does **not** download it.

1. Open the [official MediaPipe Unity Plugin v0.16.3 release](https://github.com/homuler/MediaPipeUnityPlugin/releases/tag/v0.16.3).
2. Under **Assets**, download **`com.github.homuler.mediapipe-0.16.3.tgz`**. Choose the package archive, not the source-code ZIP or TAR.GZ.
3. Place the downloaded file here, relative to the repository root:

   ```text
   FinalProject/Packages/com.github.homuler.mediapipe-0.16.3.tgz
   ```

4. Keep the exact filename and **do not extract the archive**.
5. Open or reopen the project in Unity and let package import finish.

`FinalProject/Packages/manifest.json` already references this local archive. No manual manifest changes or additional package installation are needed. The hand-landmarker model is located at `FinalProject/Assets/Resources/HandMusic/hand_landmarker.bytes` and is separate from the plugin archive.

### If Unity reports a missing package or MediaPipe compilation errors

Check that the archive is inside **`FinalProject/Packages`**, has the exact filename above, and is not an incomplete download. Restore it before retrying package resolution or reopening Unity. Without this dependency, the project may fail to compile, not just lose hand tracking.

## Git and sharing

Commit the project scripts, assets, package manifests and this README normally. Keep the MediaPipe archive ignored; do not force-add it. Every new clone or build machine must restore the archive using the instructions above. Ignoring it does not remove an existing local copy.
