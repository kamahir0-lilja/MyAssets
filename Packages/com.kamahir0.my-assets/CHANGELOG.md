# Changelog

All notable changes to this package will be documented in this file.

## [0.1.0] - 2026-11-02

### Added

- Core Editor bridge (`Lilja.MyAssets.Editor`) for diagnosing Unity Asset Store internal services, scanning local `.unitypackage` cache, searching purchased My Assets, managing downloads, and importing packages without requiring `com.unity.pipeline`.
- Optional Unity Pipeline CLI integration (`Lilja.MyAssets.Pipeline.Editor`, enabled via `versionDefines` when `com.unity.pipeline` is present) exposing `myassets_diagnose`, `myassets_cache_list`, `myassets_search`, `myassets_search_status`, `myassets_download`, `myassets_status`, `myassets_abort`, and `myassets_import`.
- Optional Setup menu (`Lilja.MyAssets.Setup.Editor`) to install `com.unity.pipeline` when absent.
