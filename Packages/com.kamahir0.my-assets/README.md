# Lilja My Assets

Unity Editor 内部の Asset Store 管理機能（My Assets）を C# および Unity CLI（`com.unity.pipeline`）から扱うためのエディター拡張パッケージです。
購入済みアセットの検索、ローカルキャッシュ（`.unitypackage`）の走査、ダウンロード開始・進捗確認・中断、プロジェクトへの非対話インポートを提供します。

## 特徴とアセンブリ構成

`com.unity.pipeline` が無いプロジェクトでも C# API としてそのまま利用できるよう、パッケージ内部を以下のアセンブリに分離しています。

- **`Lilja.MyAssets.Editor`（Core 層・`com.unity.pipeline` 非依存）**
  - 標準の `UnityEditor` モジュールのみに依存。
  - `ServicesContainer` 上の内部 Asset Store サービス（`IAssetStoreClient`, `IAssetStoreDownloadManager`, `IAssetStorePackageInstaller`, `IAssetStoreCache`, `IUnityConnectProxy` 等）をリフレクションプローブ経由で安全に解決。
  - C# API（`MyAssetsClient`）および確認用メニュー（`Lilja/MyAssets/...`）を提供。
- **`Lilja.MyAssets.Pipeline.Editor`（Pipeline CLI 層・任意連携）**
  - `versionDefines` により、プロジェクトに `com.unity.pipeline` が導入されている場合のみ有効化（`LILJA_DEPENDENCY_PIPELINE_PRESENT`）。
  - `[CliCommand]` 属性で `unity command` から呼び出せるカスタムコマンド群を自動登録。
- **`Lilja.MyAssets.Setup.Editor`（Setup 層）**
  - `com.unity.pipeline` 未導入時に `Lilja/Dependencies/MyAssets/Install Unity Pipeline` メニューを提供。

## 導入

Unity 6000.0 以上を対象とします。Package Manager の **Add package from disk** から `Packages/com.kamahir0.my-assets/package.json` を選択するか、Git URL で導入します。

```text
https://github.com/kamahir0-lilja/MyAssets.git?path=/Packages/com.kamahir0.my-assets
```

Unity CLI からカスタムコマンド（`unity command myassets_*`）を利用する場合は、対象プロジェクトに `com.unity.pipeline` を導入してください。

```sh
unity pipeline install --project-path /path/to/MyProject
```

## C# からの使用例（Core 層）

```csharp
using System.Threading;
using System.Threading.Tasks;
using Lilja.MyAssets.Editor;
using UnityEngine;

public static class MyAssetsExample
{
    public static async Task InspectAndImportAsync(CancellationToken ct)
    {
        // 1. 内部 API 解決状況とログイン状態・キャッシュパスの診断
        var diag = MyAssetsClient.Diagnose();
        Debug.Log($"Compatible={diag.IsCompatible}, LoggedIn={diag.IsUserLoggedIn}, Cache={diag.CacheDirectory}");

        // 2. オフラインでも動作するローカルキャッシュ済み .unitypackage の検索
        var cached = MyAssetsClient.ListCachedPackages("fantasy");
        foreach (var item in cached)
        {
            Debug.Log($"{item.ProductId}: {item.Title} ({item.PackagePath})");
        }

        // 3. 購入済み My Assets の検索（要ログイン）
        var searchResult = await MyAssetsClient.SearchPurchasesAsync(
            new MyAssetsQuery(searchText: "fantasy", startIndex: 0, limit: 20), ct);
        if (searchResult.IsSuccess)
        {
            foreach (var asset in searchResult.Items)
            {
                Debug.Log($"{asset.ProductId}: {asset.DisplayName} (Downloaded={asset.IsDownloaded})");
            }
        }

        // 4. ダウンロード開始・状態確認・インポート
        var dl = MyAssetsClient.StartDownload(123456);
        var status = MyAssetsClient.GetDownloadStatus(123456);
        if (status.IsCachedOnDisk)
        {
            var importResult = MyAssetsClient.ImportPackage(123456, interactive: false);
            Debug.Log($"Import success={importResult.Success}: {importResult.PackagePath}");
        }
    }
}
```

## Unity CLI からの使用例（Pipeline 層）

`com.unity.pipeline` 導入済みの Editor に対して、`unity command` から以下のコマンドを実行できます。

```sh
# 内部 API・ログイン状態・キャッシュパスの診断
unity command myassets_diagnose

# ローカルキャッシュ済み .unitypackage の一覧・キーワード検索（オフライン可）
unity command myassets_cache_list --query "fantasy"

# 購入済み My Assets の検索（非同期検索の開始、およびステータス取得）
unity command myassets_search --query "fantasy" --offset 0 --limit 25
unity command myassets_search_status

# 製品 ID を指定してダウンロード開始・進捗確認・中断
unity command myassets_download --id 123456
unity command myassets_status --id 123456
unity command myassets_abort --id 123456

# ダウンロード済みアセットのインポート（製品 ID または .unitypackage パス指定）
unity command myassets_import --id 123456
unity command myassets_import --path "/path/to/package.unitypackage"
```

## 開発と検証

リポジトリ自体が開発用 Unity プロジェクトです。配布物は `Packages/com.kamahir0.my-assets/`。

```sh
python3 scripts/validate.py
python3 scripts/run_unity.py --timeout 480 -- unity test . --mode EditMode --timeout 420 --output artifacts/editmode.xml -- -nographics -logFile artifacts/editmode-editor.log
python3 scripts/check_results.py artifacts/editmode.xml
```

## ライセンス

[MIT](LICENSE)
