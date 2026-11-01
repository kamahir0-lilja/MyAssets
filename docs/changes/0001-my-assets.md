# MyAssets ブリッジと CLI カスタムコマンドの初期仕様

Status: Adopted
Approval mode: Agent-autonomous within user-requested implementation

依頼: Asset Store の購入済みアセット（My Assets）を AI エージェントや C# エディター拡張から検索・ローカルキャッシュ照会・ダウンロード・インポートできるようにする Unity パッケージを、既存パッケージ群の流儀に倣って実装する。

採用した実装判断:
1. 配布パッケージは `com.kamahir0.my-assets` の 1 つにまとめ、内部を `com.unity.pipeline` 非依存の Core アセンブリ（`Lilja.MyAssets.Editor`）と、`versionDefines` により `com.unity.pipeline` 導入時のみ有効化される CLI アセンブリ（`Lilja.MyAssets.Pipeline.Editor`）および Setup アセンブリ（`Lilja.MyAssets.Setup.Editor`）に分離する。
2. Unity 内部の `UnityEditor.PackageManager.UI.Internal` サービス群は `ServicesContainer.instance.Resolve<T>()` に対して登録インターフェース型（`IAssetStoreClient` 等）を優先解決し、具象型（`AssetStoreClientV2` 等）へフォールバックする互換性プローブ（`AssetStoreCompatibilityProbe`）を介して呼び出す。
3. 購入一覧の非同期取得（`ListPurchases`）は `onProductListFetched` だけでは未ログイン・通信失敗時に完了しないため、`AssetStoreListOperation` の成功・エラー・終了イベントおよび取消を統合管理する。
4. インポート（`Install`）やローカル状態確認の前には `FullScanLocalInfos()` を実行し、Package Manager ウィンドウを一度も開いていないセッションでもローカルキャッシュ（`.unitypackage`）が確実に認識されるようにする。

仕様レビュー pass:
- CLI コマンドのメインスレッド制約と非同期通信の衝突を避けるため、キャッシュ一覧・診断・ダウンロード開始・状態確認・インポートはメインスレッドで完結する即時コマンドとして提供し、オンライン購入一覧の取得も開始/状態確認と非同期 C# API の両方で扱える契約とした。

正本: [my-assets](../specs/my-assets.md)。検証記録は docs/evidence に置く。
