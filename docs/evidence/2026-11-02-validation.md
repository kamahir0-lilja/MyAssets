# 初期版の検証

Status: Historical
Date: 2026-11-02

## 対象と結果

macOS arm64 / Unity 6000.6.6f1 / Unity CLI 1.0.0-beta.15 / `com.unity.pipeline` 0.8.0-exp.1。
開発プロジェクト `/Users/mahirohiratsuka/develop/unity/lilja/MyAssets` において、`com.kamahir0.my-assets` (`0.1.0`) の Core アセンブリ (`Lilja.MyAssets.Editor`)、Pipeline CLI アダプター (`Lilja.MyAssets.Pipeline.Editor`)、Setup アセンブリ (`Lilja.MyAssets.Setup.Editor`)、および EditMode テスト (`Lilja.MyAssets.Tests.Editor`) を検証した。

- EditMode: **13 passed / 0 failed / 0 skipped**。
- 実 Editor 環境の `UnityEditor.CoreModule.dll` に対して `AssetStoreCompatibilityProbe.Probe()` と `MyAssetsClient.Diagnose()` を実行し、`ServicesContainer`、`IAssetStoreClient` (`AssetStoreClientV2`)、`IAssetStoreDownloadManager`、`IAssetStorePackageInstaller`、`IAssetStoreCache`、`IUnityConnectProxy`、`IAssetStoreCachePathProxy` と必須メソッド・プロパティ・イベントが欠落なく解決されることを確認 (`RealUnityReflectionProbeResolvesCoreServices`)。
- ローカルキャッシュの走査・部分一致フィルタ・`ProductId` 昇順ソート・個別取得・範囲外 ID の検証を確認 (`ListCachedPackagesFiltersAndSortsDeterministically`, `GetCachedPackageReturnsMatchingEntryOrNull`)。
- 購入済みアセット検索のクエリ境界検証、ローカル状態との結合、未ログイン時の構造化エラー (`NOT_LOGGED_IN`)、および `CancellationToken` によるキャンセル伝播を確認 (`SearchPurchasesValidatesArgumentsAndCombinesLocalStatus`, `SearchPurchasesReturnsStructuredErrorWhenNotLoggedIn`, `SearchPurchasesCancellationStopsOperation`)。
- ダウンロード開始・進捗取得・中断・未ダウンロード時のインポート拒否・`.unitypackage` ファイル直接インポートの検証を確認 (`StartDownloadAndStatusTrackProgressAndCompletion`, `AbortDownloadCancelsActiveOperation`, `ImportPackageRequiresDownloadedFileAndInstallsWhenPresent`, `ImportPackageFileValidatesExtensionAndExistence`)。
- `Lilja.MyAssets.Editor` が `Unity.Pipeline.*` および `Lilja.MyAssets.Pipeline.Editor` を一切参照していないこと、ならびに `Lilja.MyAssets.Pipeline.Editor` の全 8 個の `[CliCommand]` メソッドが登録・委譲されることを確認 (`CoreAssemblyDoesNotReferencePipeline`, `CliCommandsAreRegisteredAndDelegateToCoreClient`)。
- 静的検査 (`python3 scripts/validate.py`) で ルート / パッケージ `README.md` の byte 単位一致、34 個のユニーク GUID と `.meta` 整合性、ドキュメント間リンク、および [仕様書](../specs/my-assets.md) の全 10 要件 ID とテストの対応を確認。

検証した `.cs` / `.asmdef` / `package.json` 21 ファイルの fingerprint:

```text
dc482d3b51b2aa84e9e7ea88d42e4d08c6727b73a50518952528be7e2b972e2e
```

相対パス UTF-8、NUL、ファイル内容をパス順で SHA-256 に入力した。

## 証跡と再現

件数、日時、report hash、fingerprint とプロセス監査の要約は [機械可読記録](2026-11-02-validation.json)。
生ログ・XML はローカルの Git 管理外 `artifacts/` に出力される。
[開発手順](../contributing/development.md) のコマンドで同じ検証を再実行できる。

## 未検証の範囲

Windows / Linux の実機、最低対応 Unity 6000.0、および Unity アカウントでオンラインサインインした状態での実 Asset Store サーバー通信と大容量パッケージの実ダウンロードは自動テストの対象外（EditMode では実 Editor の Internal サービス解決・ローカルキャッシュ走査と、`IMyAssetsBackend` 差し替えによる決定論的検証を実施）。

## Unity の終了

今回起動したバッチ Unity Editor はすべて `scripts/run_unity.py` 経由で実行し、終了時に対象 PID / PGID (`Task process cleanup verified: True`) と専用プロセスの残存なしを確認した。元から起動していた Unity Hub および共有ライセンスクライアントは操作していない。
