# Core 層と Pipeline CLI 層の分離および内部 API リフレクション設計

Status: Accepted

## 1. 単一 UPM パッケージ内でのアセンブリ分離

`com.kamahir0.my-assets` の `package.json` では外部パッケージへの必須依存を持たせず、以下の 3 つの Editor アセンブリに分割する。

- `Lilja.MyAssets.Editor`: `UnityEditor` 標準モジュールのみを参照し、リフレクションブリッジ、キャッシュ走査、非同期購入一覧検索、ダウンロード・インポート制御、C# 公開 API（`MyAssetsClient`）を提供する。
- `Lilja.MyAssets.Pipeline.Editor`: `versionDefines` で `com.unity.pipeline` の存在を検出し `LILJA_DEPENDENCY_PIPELINE_PRESENT` が定義された場合のみコンパイルする。`Unity.Pipeline.Attributes` の `[CliCommand]` / `[CliArg]` を用いて Core 層を CLI コマンドとして公開する。
- `Lilja.MyAssets.Setup.Editor`: `com.unity.pipeline` が未導入の場合のみ `Lilja/Dependencies/MyAssets/Install Unity Pipeline` メニューを提供する。

これにより、`com.unity.pipeline` を使わないプロジェクトや単体テストでも Core 層がそのまま機能し、かつ `com.unity.pipeline` 側の仕様変更が Core 層へ波及しない。

## 2. `ServicesContainer` のインターフェース解決と互換性プローブ

Unity 6（`UnityEditor.CoreModule.dll` 内の `UnityEditor.PackageManager.UI.Internal`）では、`AssetStoreClientV2` 等の各サービスは `BaseService<TInterface>` を継承しており、`ServicesContainer.Register` 時にインターフェース型（`IAssetStoreClient`, `IAssetStoreDownloadManager`, `IAssetStorePackageInstaller`, `IAssetStoreCache`, `IUnityConnectProxy`, `IAssetStoreCachePathProxy`）をキーとして登録される。

そのため、具象型名だけで `Resolve<AssetStoreClientV2>()` を呼ぶと解決に失敗する。`AssetStoreCompatibilityProbe` ではインターフェース型での `Resolve<TInterface>()` を優先し、失敗時に具象型および `m_RegisteredServices` の走査へフォールバックする。

## 3. テスト容易性のための内部バックエンド境界

実 Unity 環境の非公開 API 互換性を検証するテストと、ネットワークや Unity アカウントのログイン状態に依存しない決定論的な受け入れテストを両立するため、内部に `IMyAssetsBackend` インターフェースを設ける。通常時は `UnityAssetStoreReflectionBackend` を使用し、テスト時は必要に応じて差し替え可能とする。
