# MyAssets の開発

ルートは開発用 Unity プロジェクト。配布物は `Packages/com.kamahir0.my-assets/`。
入口は [docs/README.md](docs/README.md)。ルートとパッケージの README は同一に保つ。

- 振る舞いを変えるときは [ワークフロー](docs/contributing/workflow.md) に従い、利用例、期待結果、取消・失敗、受け入れテストを先に具体化する。
- 仕様の正本は `docs/specs/`。仕様に合わない不具合の修正は、仕様変更を作らず回帰テストと修正を行う。
- 段階の移行だけを理由に承認待ちにしない。依頼内の判断は理由を残して進める。別エージェントや外部への公開を必須にしない。
- `python3 scripts/validate.py` と変更に対応した Unity テストを実行する。仕様・テストの対応を保ち、未実行を合格にしない。
- `com.unity.pipeline` 非依存の Core アセンブリ（`Lilja.MyAssets.Editor`）と、`com.unity.pipeline` 導入時のみ有効化される CLI アセンブリ（`Lilja.MyAssets.Pipeline.Editor`）の分離を維持する。
- 起動前に既存 Editor と対象 project を識別する。今回起動した Editor は保存・通常終了し、対象 PID と専用プロセスの終了を確認する。既存 Editor は閉じない。
- バッチ実行は `scripts/run_unity.py` で上限を付ける。ホスト異常終了時まで cleanup を保証したとは言わない。最終報告に Unity の終了状態を記載する。

ビルド・テスト・依存関係の導入手順は [開発手順](docs/contributing/development.md) を参照する。
