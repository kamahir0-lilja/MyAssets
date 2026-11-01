---
name: unity-my-assets
description: Search, download, inspect, and import purchased Unity Asset Store packages (My Assets / .unitypackage) via Unity CLI (com.unity.pipeline) and MyAssets commands.
---

# unity-my-assets

## 目的

AI エージェントが Unity CLI（`unity command`）および `com.kamahir0.my-assets` パッケージ経由で、購入済み Unity Asset Store パッケージ（My Assets）の診断、検索、ダウンロード、ローカルキャッシュ確認、およびプロジェクトへの非対話インポートを自動化するための手順。

## 発動トリガー

次のようなユーザーの要求時に本スキルを使用する:

- 「購入した Asset Store のアセット（例: DOTween, Odin, Bakery 等）をこのプロジェクトに入れて」
- 「Asset Store の購入済みアセット（My Assets）を検索して」
- 「ローカルにキャッシュされている `.unitypackage` を一覧表示して」
- 「Asset Store パッケージのダウンロード進捗を確認 / 中断して」
- 「`.unitypackage` ファイルを直接プロジェクトにインポートして」

> **注意**: 公式の `unity-package-management` スキルは UPM（`com.unity.*` や git URL / scoped registry）用です。Asset Store で購入した `.unitypackage` の管理には本スキル（`myassets_*` コマンド群）を使用します。

## 前提条件とセットアップ

対象の Unity プロジェクトで `myassets_*` CLI コマンドを使用するには、以下の 2 つが必要です:
1. `com.unity.pipeline`（Unity CLI コマンド実行用）
2. `com.kamahir0.my-assets`（My Assets ブリッジ & CLI アダプター）

### 1. コマンド利用可能確認

プロジェクトのルートで以下を実行し、`myassets_diagnose` が登録されているか確認する:

```bash
unity cmd list
```

### 2. 未導入時の自動セットアップ手順

もし `myassets_*` コマンドが存在しない場合は、プロジェクトへ導入する:

1. **`com.unity.pipeline` の導入**:
   ```bash
   unity pipeline install --project-path .
   ```
2. **`com.kamahir0.my-assets` の UPM 追加**:
   `Packages/manifest.json` の `dependencies` に以下を追加する:
   ```json
   "com.kamahir0.my-assets": "https://github.com/kamahir0-lilja/MyAssets.git?path=/Packages/com.kamahir0.my-assets"
   ```

## 基本ワークフロー

### Step 1: 診断（ログイン状態とキャッシュ確認）

まず `myassets_diagnose` を実行して、内部 API の互換性、Unity アカウントのログイン状態、ローカルキャッシュディレクトリを確認する:

```bash
unity cmd run myassets_diagnose
```

- `isCompatible`: `true` であることを確認。
- `isUserLoggedIn`: `true` であればオンライン検索・ダウンロードが可能。`false` の場合はオンライン API は使えないため、ローカルキャッシュ走査（Step 2）のみ行うか、ユーザーに Unity Hub / エディタからのログインを案内する。

### Step 2: キャッシュ済みアセットの確認（オフライン対応）

すでにローカルディスクにダウンロードされている `.unitypackage` を探す場合（未ログイン時でも動作）:

```bash
unity cmd run myassets_cache_list --query "<キーワードまたは製品ID>"
```

- 返却される `items` の `fileExists: true` かつ `packagePath` が存在すれば、ダウンロード不要で即座に Step 4 のインポートを実行可能。

### Step 3: 購入済み My Assets の検索（要ログイン）

ユーザーの購入済みアセット一覧から検索する:

```bash
unity cmd run myassets_search --query "<アセット名>" --offset 0 --limit 25
```

- レスポンスの `state` が `"completed"` の場合、`items` に検索結果（`productId`, `displayName`, `isDownloaded`, `localVersion` など）が含まれる。
- もし `state` が `"in_progress"` と返ってきた場合は、メインスレッドでの非同期取得中であるため、1〜2 秒待ってからステータス確認コマンドを実行する:
  ```bash
  unity cmd run myassets_search_status
  ```

### Step 4: ダウンロードと進捗確認

対象アセットの `productId` を指定してダウンロードを開始する:

```bash
unity cmd run myassets_download --id <productId>
```

ダウンロードの進捗ポーリング:
```bash
unity cmd run myassets_status --id <productId>
```

- `progress`: 0.0 〜 1.0
- `isCachedOnDisk`: `true` になればダウンロード完了。
- 中断が必要な場合:
  ```bash
  unity cmd run myassets_abort --id <productId>
  ```

### Step 5: プロジェクトへのインポート

ダウンロード完了後、またはローカルに存在するアセットをプロジェクトへ非対話インポートする:

**製品 ID でインポート（ローカルキャッシュから自動検索）**:
```bash
unity cmd run myassets_import --id <productId>
```

**`.unitypackage` ファイルパスを直接指定してインポート**:
```bash
unity cmd run myassets_import --path "/path/to/asset.unitypackage"
```

インポート成功後、Unity の AssetDatabase が更新され、プロジェクト内にアセットが展開される。

## Agent ガイドライン

- **即時インポートの禁止**: `isCachedOnDisk: false` の状態で `myassets_import` を実行しないこと。必ず `myassets_status` でキャッシュ完了を確認してからインポートを呼び出す。
- **未ログイン時のフォールバック**: `isUserLoggedIn: false` の場合は、`myassets_search` をリトライせず、`myassets_cache_list` で目的のアセットが既にキャッシュされていないか確認する。キャッシュになければユーザーにログイン状態の確認を依頼する。
- **引数のバリデーション**: `--id` は必ず正の整数（`productId > 0`）を指定する。
