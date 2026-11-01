# 文書の入口

- [現行仕様と受け入れ条件](specs/my-assets.md): 利用者および AI / CLI から観察できる契約の正本。
- [利用例から実装するワークフロー](contributing/workflow.md): 変更、レビュー、テスト、完了の順序。
- [開発・検証](contributing/development.md): 環境の導入と再現可能なコマンド。
- [初期仕様の採用記録](changes/0001-my-assets.md): 依頼と採用した判断の区別。
- [Core 層と Pipeline CLI 層の分離に関する設計判断](adr/0001-reflection-core-and-pipeline-split.md): アセンブリ構成と内部 API ブリッジ方式を選んだ理由。
- [初期版の検証記録](evidence/2026-11-02-validation.md): EditMode テスト結果と確認範囲。

現在の期待結果は specs、採用理由は changes / adr、実行時点の検証結果は evidence に置く。
過去の検証結果を現在の保証へ読み替えない。生ログは Git 管理外の `artifacts/` に保存する。
