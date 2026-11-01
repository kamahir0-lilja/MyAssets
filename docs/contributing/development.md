# 開発・検証

Status: Active

開発 Editor は `ProjectSettings/ProjectVersion.txt`、実行結果は evidence を参照する。
初回 checkout は `Packages/manifest.json` に `com.unity.pipeline` と `com.unity.test-framework` を含む。

## 静的確認と Unity テスト

```sh
python3 scripts/validate.py
python3 scripts/run_unity.py --timeout 480 -- unity test . --mode EditMode --timeout 420 --output artifacts/editmode.xml -- -nographics -logFile artifacts/editmode-editor.log
python3 scripts/check_results.py artifacts/editmode.xml
```

`run_unity.py` は macOS / Linux 用。今回起動する command を別 process group にし、期限内の通常終了を待つ。
終了後は group 内の PID を調べ、残った専用プロセスへ TERM、猶予後のみ KILL を送る。
起動前の既存 Editor は別途確認する。group 外へ離脱したプロセスや host 自体の異常終了まで cleanup を保証しない。
最終確認では Unity Editor 本体とこの project の専用プロセスをプロセス一覧でも確認する。

## アセンブリ構成

- `Lilja.MyAssets.Editor`: `com.unity.pipeline` 非依存の Core アセンブリ。標準の `UnityEditor` のみを参照する。
- `Lilja.MyAssets.Pipeline.Editor`: `versionDefines` により `com.unity.pipeline` 導入時のみ `LILJA_DEPENDENCY_PIPELINE_PRESENT` が定義され有効化される CLI コマンドアセンブリ。
- `Lilja.MyAssets.Setup.Editor`: `com.unity.pipeline` 未導入時に `Lilja/Dependencies/MyAssets/Install Unity Pipeline` メニューを表示する Setup アセンブリ。
