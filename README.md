# AIDD Project Template

配布版: **5**

過剰な設計や文書作成を抑え、動作するモックで仕様合意を形成してから実処理へ接続する「モック駆動開発」のテンプレートです。

特定の言語・フレームワーク・開発ツールに依存せず、プロジェクトの要件に合わせて柔軟に利用できます。本リポジトリから [template/](template/) の内容と [LICENSE](LICENSE) をコピーして利用します。

## 1. 配布物と役割

| ファイル | 役割 |
| --- | --- |
| [README.md](template/README.md) | プロジェクトの概要と実行手順への案内 |
| [AGENTS.md](template/AGENTS.md) | AIエージェントが参照する行動規範。現在地、停止点、文書の扱い、完了報告 |
| [PLAN.md](template/PLAN.md) | 現在地、未決事項、ユースケースの進捗（状態語）、再開情報 |
| [docs/document-policy.md](template/docs/document-policy.md) | 採用記録、モック適用範囲、正本の配置、保護する合意 |
| [docs/project.md](template/docs/project.md) | 目的・制約、ユースケースと合意記録、確認した事実、実行手順、合否表 |
| [docs/architecture.md](template/docs/architecture.md) | 全体設計の合意、システムコンテキスト、コンテナ、実現パターン、設計判断 |
| [docs/standards/design-and-documentation.md](template/docs/standards/design-and-documentation.md) | 設計と文書化の基準、先行してよい成果物、完了基準、変更手続き |
| [docs/standards/mock-driven-development.md](template/docs/standards/mock-driven-development.md) | ユースケースごとの段階とゲート条件、仕掛かりの上限、モックの境界 |
| [scripts/doc_check.py](template/scripts/doc_check.py) | 文書整合の判定 7 件（リンク、済みチェック、絶対パス、仕掛かりと合意記録、UC ID、標準のハッシュ、証跡と行数の報告）。Python 3 標準ライブラリのみ |

ユースケースや確認した事実は `docs/project.md`、全体構造と設計判断は `docs/architecture.md` を正本とします。これら以外の規約・方針文書は新設しません。`docs/standards/` は配布元からの輸入物として編集せず、差分は `docs/document-policy.md` 第1節に記録します。

`scripts/doc_check.py` の実行には Python 3 が必要です。Python を使わない構成では導入を見送り、その旨を `docs/document-policy.md` 第1節の差分欄に記録します。本テンプレートは [MIT ライセンス](LICENSE) で配布しているため、コピー時も著作権表示と許諾文を保持してください。

## 2. 新規プロジェクトへの導入

本リポジトリを取得し、`template/` の内容と `LICENSE` を新プロジェクトのルートへコピーします。

PowerShell での実行例（`my-project` は任意のプロジェクト名）:

```powershell
$templateSource = (Resolve-Path -LiteralPath './template').Path
$projectTarget = Join-Path (Split-Path -Parent (Get-Location).Path) 'my-project'
if (Test-Path -LiteralPath $projectTarget) {
    throw 'コピー先が存在します。新しい名前を指定するか、第4節の既存プロジェクト向け手順を使ってください。'
}
New-Item -ItemType Directory -Path $projectTarget | Out-Null
Get-ChildItem -LiteralPath $templateSource -Force | Copy-Item -Destination $projectTarget -Recurse
Copy-Item -LiteralPath './LICENSE' -Destination $projectTarget
Set-Location -LiteralPath $projectTarget
```

bash での実行例（コピー先が既に存在する場合は `mkdir` が失敗して停止します）:

```bash
mkdir ../my-project && cp -r template/. ../my-project && cp LICENSE ../my-project && cd ../my-project
```

コピー後に `python scripts/doc_check.py` を実行し、リンクとアンカーの整合性を確認します。コピー後の文書はプロジェクト内で完結して参照するため、配布元リポジトリとの同期設定は不要です。

AIエージェントを利用する場合は、プロジェクト側の `AGENTS.md` が読み込まれるよう設定します。ツール固有の設定ファイルが必要な場合も、規則を複製せず参照にとどめます。例えば Claude Code では、ルートに `@AGENTS.md` と記載した `CLAUDE.md` を配置します（詳細は [公式ドキュメント](https://code.claude.com/docs/en/memory) を参照）。

```markdown
@AGENTS.md
```

## 3. 初期セットアップと最初のユースケース

以下の順序で、最初のユースケース 1 件を実処理まで通します。未着手ユースケースの画面や系列を先行して準備する必要はありません。

| 順序 | 作業内容 | 完了条件 |
| --- | --- | --- |
| 1 | `docs/document-policy.md` に採用記録、モック適用範囲の固有の除外、差分を記入 | 採用規則と適用範囲が確定し、状態を「適用済み」に更新 |
| 2 | `README.md`（概要）、`docs/project.md` 第1・2節（目的、対象、制約、受け入れ条件）、第3節のカタログ表（UC ID、主アクター、目的、実装順序）を記入 | 解決する問題と対象外を説明でき、UC-1 が決まっている |
| 3 | アーキテクチャを決定づける外部依存の実測を行い、事実を `docs/project.md` 第4節と `docs/reference/` に記録 | 情報源・対象版・確認日が記録されている（該当する外部依存がなければ省略） |
| 4 | `docs/architecture.md` 第1・2節、パターンの名称と適用条件、第4節の決定を記入し、全体設計の合意欄を埋める | 合意欄に提示コミットと利用者の応答の原文がある |
| 5 | UC-1 を [モック標準第2節](template/docs/standards/mock-driven-development.md#workflow) の段階1〜5で通す。パターン本体はここで書く | `docs/project.md` 第6節の合否表と `PLAN.md` が更新され、UC-1 が完了 |
| 6 | 次のユースケースへ順序5を繰り返す。`docs/architecture.md` は構造が変わるときのみ再訪 | 段階1〜3にあるユースケースが常に1件以下 |

### 補足事項
- **モック対象外の変更**: 段階2・3を省略し、段階4・5（実装と検証）を実施します。文書のみの変更は `scripts/doc_check.py` の実行で整合性を確認します。
- **記入欄 `{{...}}` の扱い**: 初期段階ですべて埋める必要はありません。未確定事項は推測で埋めず、`PLAN.md` で管理します。

## 4. 既存プロジェクトへの導入

既存の構成や要件に合わせて、必要な要素を段階的に取り込みます。

1. **正本の対応付け**: 既存文書の役割を確認し、テンプレートの各責務に対応付けます（既存文書がある場合、`docs/project.md` への転記は不要です）。
2. **標準の差分管理**: 既存の規約と競合する場合は理由と影響を確認し、合意済みの差分のみを `docs/document-policy.md` 第1節に記録します。`docs/standards/` の本文は書き換えません。
3. **文書方針への記録**: `docs/document-policy.md` に採用した版や適用日、差分を記録します。テンプレートの構成に全面的に合わせる必要はありません。
4. **段階的な適用**: 直近で変更する小さなユースケース 1 件から第3節の順序5を適用します。既存機能の遡及的なユースケース化やモック化、不要なリファクタリングは行いません。

## 5. 配布版の更新取り込み

本テンプレートの新版を取り込む際は自動適用を避け、第7節の変更履歴で変更点を確認したうえで必要な差分のみを反映します（手順は第4節と同様）。プロジェクト固有の仕様や検証結果を上書きしないよう注意し、反映後は `docs/document-policy.md` の採用版を更新します。

## 6. 導入後の確認事項

- `scripts/doc_check.py` の出力の NG を確認し、`docs/document-policy.md` 第1節に記録された合意済みの採用差分に該当するものを除いて解消していること。
- 決定事項の正本が1箇所に定まり、リンクで参照できること。
- 不要な文書・階層・依存関係が増加していないこと。
- モック対象のユースケースにおいて、系列・再現手順・合意記録が整合していること。
- 実処理への接続が確認され、未検証の項目が明記されていること。
- 未決事項が残っているユースケースが誤って完了扱いになっていないこと。

## 7. 変更履歴

配布版は `template/` の内容が変わるたびに上がります。標準の版は、その標準自体に変更があった場合のみ上がります。

### 版5

全文書の表現を整理しました。ユースケースの分割条件、設計・検証に必要な項目、モックの制約は維持しています。

- 変更したファイル: 全 Markdown 文書（10ファイル）および `scripts/doc_check.py`。
- `AGENTS.md`（ルート・テンプレート共）: 保守時の注意点、作業規範、停止点を維持して表現を簡潔化。
- `PLAN.md`: 冒頭と各節で重複していた「仕様・設計・検証結果は書かない」旨の記述を集約。
- `README.md`（ルート・テンプレート共）: 正本参照や補足事項の重複記述を整理。
- `docs/architecture.md`: 標準と重複する記述を整理し、設計判断の根拠と保存方針を明確化。
- `docs/document-policy.md`: 配布元・標準の版を更新（配布版5、標準各版4）。正本の配置と保護対象の記述を整理。
- `docs/project.md`: ユースケース定義や合意記録の要件、検証結果の運用ルールを平易に集約。
- `docs/standards/design-and-documentation.md`（版4）: 原則、抑止基準、文書の上限、先行成果物の範囲、完了基準を維持して表現を整理。
- `docs/standards/mock-driven-development.md`（版4）: ワークフロー表および補足項目の重複記述（合意条件、仕掛かり上限、モック境界等）を集約。
- `scripts/doc_check.py`: 標準文書の改訂に伴い、正規化ハッシュの期待値を更新。
- 標準の版: 設計・文書標準 4、モック標準 4。
- 採用側への影響: 規則や判定仕様の変更はありません。既存プロジェクトへ取り込む場合は、第5節に従って必要な差分を反映します。標準文書を更新する場合は、対応する `scripts/doc_check.py` の期待ハッシュも更新し、`docs/document-policy.md` に採用版を記録します。配布物の総行数は Markdown 388 行（版4 は 388 行）、スクリプト 406 行です。

### 版4

2 つの採用プロジェクトの実態調査に基づく再構成です。規則が「読まれたが止める力を持たなかった」ことへの対応として、ゲートを状態語と合意記録で判定できる形にし、文書を追記型の器から現在の状態だけを持つ表に変え、最小の判定スクリプトを同梱しました。

- 変更したファイル: 全 7 文書を改訂。`docs/architecture.md` と `scripts/doc_check.py` を新設。
- `AGENTS.md`: 現在地は `PLAN.md` 第1節、停止点は全体設計の合意とユースケースの動作合意の2つ、標準は編集しない、文書は現在の状態だけ、未検証のまま状態語を進めない、の作業原則に置換。横展開を許可していた「未決事項に依存しない作業は先行して進めます」を削除。
- `PLAN.md`: チェックボックスの作業一覧と機能の進捗表を、現在地3行、未決事項表、状態語によるユースケース進捗表、再開情報に置換。
- `docs/document-policy.md`: モック適用範囲を「ユースケースの系列を新設・変更する作業」に置換。正本表に `architecture.md` と `reference/` を追加。表にない規約・方針・プロセス文書の新設を禁止。図を Mermaid に固定。保護する合意に全体設計の合意欄と設計判断 ID を追加。
- `docs/project.md`: 第3節を機能 F からユースケース（カタログ表、粒度規則、系列 ID、合意記録の4項目）に置換。第4節を確認した事実・技術選定・パターンからの逸脱に縮小。第6節を更新のみ行う合否表に置換。
- `docs/architecture.md`（新設）: 全体設計の合意欄、システムコンテキスト、コンテナ、実現パターン（役割表、シーケンス図、整合性、モック境界）、出所付きの設計判断表。
- `docs/standards/design-and-documentation.md`（版3）: 標準を読み取り専用の輸入物と明記。文書は現在の状態のみ、証跡の形式、リポジトリに置かない成果物と外部実測の例外、採用後の行数上限、全体設計と先行してよい成果物、ADR の条件、規約改訂の分離、必須コミット2つ、第6節末尾の許可文の置換。
- `docs/standards/mock-driven-development.md`（版3）: 段階の「次へ進める条件」を判定可能な条件に置換。合意記録の4項目と返答様式、仕掛かりの上限、合意待ち中に進めてよい作業、モック範囲の上限、境界規則（本番の型を共有する合成点1箇所、固定表、段階4完了時の削除）、記録先の表。「独立した機能を進めます」を削除。
- `scripts/doc_check.py`（新設）: 判定7件。相対リンクとアンカー、済みチェックボックス、ローカル絶対パス、仕掛かり件数と合意記録の照合、UC ID の一致、標準の正規化ハッシュ、証跡ファイルと行数の報告。
- 標準の版: 設計・文書標準 3、モック標準 3。
- 採用側への影響: 文書構成が変わるため、差し替えではなく第4節の手順で段階的に取り込みます。機能 F はユースケースに読み替え、`PLAN.md` は新しい構成で作り直します。`docs/standards/` を改変している採用側では判定 6 が NG になるので、配布元の本文に戻し、差分を採用記録に移します。アンカー ID は `#features` と `#f1` を廃止し `#usecases` と `#uc-1` を追加しました。配布物の総行数は Markdown 388 行（版3 は 311 行）、スクリプト 406 行です。

### 版3

- `docs/document-policy.md`: 採用記録の「配布元・版」に本リポジトリへのリンクを追加。
- `AGENTS.md`: 「作業原則」の見出し階層を修正（`###` → `##`）。
- 標準の版: 設計・文書標準 2、モック標準 2（変更なし）。
- 採用側への影響: なし。`docs/document-policy.md` 第1節の配布元・版を更新するだけで済みます。

### 版2

版2 は同じ版番号のまま 2 回改訂されています（コミット `cdb3e29` → `d0e4a88`）。本節は最終状態を指します。

- 全ファイル: 文体と用語を統一し、説明文を箇条書きに整理。
- `AGENTS.md`: 参照表に「層・抽象化・依存関係などの追加」「導入、規約・正本配置の変更」を追加。作業原則を3項目に整理し、完了基準への参照を追加。
- `docs/document-policy.md`: 正本の表に「既存の検証記録」を追加。`#adoption` アンカーを追加。
- `docs/project.md`: 第6節に外部の検証記録を参照する方針を追加。
- `docs/standards/design-and-documentation.md`（版2）: 実装原則5項目を第2節から第1節へ移し、`#implementation-principles` アンカーを追加。完了条件とレビュー指摘の分類を箇条書きに整理。
- `docs/standards/mock-driven-development.md`（版2）: 「技術検証の先行」を追加。適用範囲の定義先を文書方針へリンク。
- 採用側への影響: 版1 から取り込む場合は `docs/standards/` の2文書と `AGENTS.md` の差し替えを推奨。アンカー ID の削除はないため、記入済みの内容からのリンクは維持されます。

### 版1

初版。
