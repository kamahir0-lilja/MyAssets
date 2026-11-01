# MyAssets ブリッジと CLI カスタムコマンド

Status: Adopted

対象: `com.kamahir0.my-assets` 0.1.0 / Unity 6000.0 以上。
開発用 Editor は `ProjectSettings/ProjectVersion.txt` を参照。`com.unity.pipeline` は任意連携（導入時のみ CLI コマンドを有効化）。

## 利用シナリオ

1. AI エージェントや開発者が `MyAssetsClient.Diagnose()` または `unity command myassets_diagnose` を実行し、現在の Unity Editor で内部 Asset Store サービスが解決可能か、ログイン状態、キャッシュパスを確認する。
2. オフラインまたは未ログイン状態でも、`MyAssetsClient.ListCachedPackages("fantasy")` または `unity command myassets_cache_list --query fantasy` でローカルにダウンロード済みの `.unitypackage` 一覧を検索・確認する。
3. ログイン済みの Editor で `MyAssetsClient.SearchPurchasesAsync(new MyAssetsQuery("fantasy", 0, 20))` または `unity command myassets_search --query fantasy` を実行し、購入済み My Assets 一覧と各アセットのダウンロード済み・インポート済み状態を取得する。
4. 製品 ID を指定して `MyAssetsClient.StartDownload(productId)` / `GetDownloadStatus(productId)` / `AbortDownload(productId)`（または対応する CLI コマンド）でダウンロード開始・進捗確認・中断を行う。
5. ダウンロード済みの製品 ID または `.unitypackage` ファイルパスを指定し、`MyAssetsClient.ImportPackage(productId)` / `ImportPackageFile(path)`（または `unity command myassets_import`）で非対話インポートを実行する。

## 契約と受け入れ条件

| ID | 契約 | 受け入れテスト |
| --- | --- | --- |
| MA-DIAG-001 | `AssetStoreCompatibilityProbe` と `MyAssetsClient.Diagnose()` は Unity 6 の内部型と `ServicesContainer` サービス解決可否を検査し、例外を投げずに診断結果を返す | RealUnityReflectionProbeResolvesCoreServices / DiagnoseReportsBackendState |
| MA-CACHE-001 | `ListCachedPackages` / `GetCachedPackage` はローカル情報を再走査し、キーワードによる部分一致フィルタと `productId` 昇順ソートを行い、未ログイン時でも動作する | ListCachedPackagesFiltersAndSortsDeterministically / GetCachedPackageReturnsMatchingEntryOrNull |
| MA-SEARCH-001 | `SearchPurchasesAsync` は `startIndex >= 0`・`limit > 0` を検証し、購入情報にローカルキャッシュ・インポート状態を結合して返す | SearchPurchasesValidatesArgumentsAndCombinesLocalStatus |
| MA-SEARCH-002 | `SearchPurchasesAsync` は未ログインや API エラー時にハングせず構造化エラー（`IsSuccess = false`）を返し、CancellationToken 取消時は進行中検索を停止して `OperationCanceledException` で完了する | SearchPurchasesReturnsStructuredErrorWhenNotLoggedIn / SearchPurchasesCancellationStopsOperation |
| MA-DL-001 | `StartDownload` / `GetDownloadStatus` は正の `productId` を検証し、ダウンロード開始要求と進捗・状態・キャッシュ有無を返す | StartDownloadAndStatusTrackProgressAndCompletion |
| MA-DL-002 | `AbortDownload` は正の `productId` に対して中断要求を送り、進行中操作がない場合は `false` を返す | AbortDownloadCancelsActiveOperation |
| MA-IMPORT-001 | `ImportPackage(productId)` はインポート前にローカルキャッシュを走査し、未ダウンロードまたはファイル欠損時は例外ではなく `not_downloaded` エラー結果を返す。存在する場合は非対話インストールを実行する | ImportPackageRequiresDownloadedFileAndInstallsWhenPresent |
| MA-IMPORT-002 | `ImportPackageFile(packagePath)` は空パス・存在しないファイル・`.unitypackage` 以外の拡張子を拒否し、有効なファイルのみインポートする | ImportPackageFileValidatesExtensionAndExistence |
| MA-CLI-001 | `Lilja.MyAssets.Editor` は `com.unity.pipeline` への参照を持たず、Pipeline 層（`Lilja.MyAssets.Pipeline.Editor`）のみが `versionDefines` で `com.unity.pipeline` を参照する | CoreAssemblyDoesNotReferencePipeline |
| MA-CLI-002 | `MyAssetsCliCommands` は診断・キャッシュ一覧・購入検索・ダウンロード開始・ステータス確認・ダウンロード中断・インポートの `[CliCommand]` を公開し、Core 層と同じ契約で JSON シリアライズ可能な結果を返す | CliCommandsAreRegisteredAndDelegateToCoreClient |

## 公開 API

### Core 層（`Lilja.MyAssets.Editor` 名前空間、`com.unity.pipeline` 非依存）

- `MyAssetsClient.Diagnose()`
- `MyAssetsClient.ListCachedPackages(string? query = null, bool refresh = true)`
- `MyAssetsClient.GetCachedPackage(long productId, bool refresh = true)`
- `MyAssetsClient.SearchPurchasesAsync(MyAssetsQuery query, CancellationToken cancellationToken = default)`
- `MyAssetsClient.StartPurchaseSearch(MyAssetsQuery query)` / `MyAssetsClient.GetLastPurchaseSearchStatus()`
- `MyAssetsClient.StartDownload(long productId)` / `MyAssetsClient.StartDownloads(IEnumerable<long> productIds)`
- `MyAssetsClient.GetDownloadStatus(long productId)`
- `MyAssetsClient.AbortDownload(long productId)`
- `MyAssetsClient.ImportPackage(long productId, bool interactive = false)`
- `MyAssetsClient.ImportPackageFile(string packagePath, bool interactive = false)`

### Pipeline CLI 層（`Lilja.MyAssets.Pipeline.Editor` 名前空間、`com.unity.pipeline` 導入時のみ有効）

- `myassets_diagnose`: 内部 API 解決状況・ログイン状態・キャッシュパスの診断
- `myassets_cache_list`: ローカルキャッシュ済み `.unitypackage` の一覧・検索
- `myassets_search`: 購入済み My Assets の検索（非同期開始＋完了待機またはステータス取得）
- `myassets_search_status`: 進行中または直近の購入検索ジョブの結果取得
- `myassets_download`: 指定した製品 ID のダウンロード開始
- `myassets_status`: 指定した製品 ID のダウンロード進捗・ローカルキャッシュ状態確認
- `myassets_abort`: 指定した製品 ID のダウンロード中断
- `myassets_import`: 指定した製品 ID または `.unitypackage` パスのプロジェクトへのインポート

## 保証の境界

- Unity Editor 専用（`includePlatforms: ["Editor"]`）。Player ビルドには含まれない。
- 購入済み一覧のオンライン検索と新規ダウンロードには、対象 Unity Editor が Unity アカウントにログイン済みであり、Asset Store 利用規約への同意と対象アセットの所持権限が必要。
- ローカルキャッシュ一覧（`ListCachedPackages`）とキャッシュ済み `.unitypackage` のインポートはオフラインでも動作する。
- `UnityEditor.PackageManager.UI.Internal` は Unity 内部実装であるため、将来の Unity バージョンで内部構造が変更された場合は `AssetStoreCompatibilityProbe` と `Diagnose()` が非互換項目を報告する。
