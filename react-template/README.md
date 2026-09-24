# React テンプレート

新規プロジェクトは配布元ルートの `mise run init:react <新しい出力先>` で生成します。生成先ルートには React・Node.js・SQLite の実行可能なアプリ一式が入り、メモの参照実装は `reference/` に残します。製品の現行仕様はルートの `docs/` に記述し、メモのユースケース・シナリオと設計は `reference/docs/` に保持します。

生成先ルートで `npm run setup` を実行し、`npm run dev` で製品アプリを起動するか `npm run verify` で検証します。サンプルは `reference/` へ移動して同じコマンドを実行します。`.github/workflows/react-template.yml` は生成先ルートと `reference/` の両方を検証する CI 例です。

サンプルの構成、実行手順、ユースケースと、製品名へ置き換える箇所は [reference/README.md](reference/README.md) から参照してください。共通の `AGENTS.md`、標準、文書検査スクリプトは生成時に `template/` からルートへ配置します。生成時に依存取得やビルドは行わず、既存の出力先は上書きしません。`react-template/` は単体では実行せず、生成先で開発・検証します。
