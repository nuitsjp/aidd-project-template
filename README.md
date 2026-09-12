# AIDD Project Template

配布版: **3**

過剰なドキュメント作成や設計（オーバーエンジニアリング）を抑え、動作するモックで仕様を合意してから実処理へ接続する「モック駆動開発」のテンプレートです。

特定の言語・フレームワーク・開発ツールに依存せず、プロジェクトの要件に合わせて柔軟に利用できます。本リポジトリから [template/](template/) の内容と [LICENSE](LICENSE) をコピーして利用します。

## 1. 配布物と役割

| ファイル | 役割 |
| --- | --- |
| [README.md](template/README.md) | プロジェクトの概要と実行手順への案内 |
| [AGENTS.md](template/AGENTS.md) | AIエージェントが参照する行動規範 |
| [PLAN.md](template/PLAN.md) | 進捗状況、未決・未実装・未検証事項の管理 |
| [docs/document-policy.md](template/docs/document-policy.md) | 採用標準、モック適用範囲、正本の役割 |
| [docs/project.md](template/docs/project.md) | プロジェクト固有の要件・仕様・設計・実行検証 |
| [docs/standards/design-and-documentation.md](template/docs/standards/design-and-documentation.md) | 設計と文書化の基準、検証および変更管理 |
| [docs/standards/mock-driven-development.md](template/docs/standards/mock-driven-development.md) | モック作成、仕様合意、実処理接続の手順 |

プロジェクト固有の仕様や設計は、まず `docs/project.md` に集約します。個別文書への分割は管理上の必要が生じた段階で行ってください（初期段階で空の文書を用意する必要はありません）。

同梱の標準文書は製品固有の要件と分けて管理します。本テンプレートは [MIT ライセンス](LICENSE) で配布しているため、コピー時も著作権表示と許諾文を保持してください。

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

コピー後の文書はプロジェクト内で完結して参照するため、配布元リポジトリとの同期設定は不要です。

AIエージェントを利用する場合は、プロジェクト側の `AGENTS.md` が読み込まれるよう設定します。ツール固有の設定ファイルが必要な場合も、規則を複製せず参照にとどめます。例えば Claude Code では、ルートに `@AGENTS.md` と記載した `CLAUDE.md` を配置します（詳細は [公式ドキュメント](https://code.claude.com/docs/en/memory) を参照）。

```markdown
@AGENTS.md
```

## 3. 初期セットアップと最初の機能実装

以下の手順で、最初の小さな機能を実処理まで実装します。すべての設計やモックを先行して揃える必要はありません。

| 順序 | 作業内容 | 完了条件 |
| --- | --- | --- |
| 1 | `docs/document-policy.md` に採用記録、モック適用範囲、差分を記入 | 採用規則と適用範囲が確定し、状態を「適用済み」に更新 |
| 2 | `README.md`（概要）と `docs/project.md` 第1・2節（目的、対象、制約、受け入れ条件）を記入 | 解決する問題と対象外の範囲を説明できる |
| 3 | `docs/project.md` 第3・4節に最初の機能のシナリオ、外部仕様、責務と境界を記入 | 事実と仮定が区別され、検証方法が決まっている |
| 4 | 対象機能に [モック駆動開発](template/docs/standards/mock-driven-development.md#workflow) を適用し、再現手順を `docs/project.md` 第5節へ記入 | 動作するモックで合意を形成し、第3節に記録している |
| 5 | 合意した機能を実装して実処理へ接続。手順を第5節、結果を第6節へ記入 | 検証に合格し、モック無効時も期待通り動作する |
| 6 | `PLAN.md` を更新し、差分を [完了基準](template/docs/standards/design-and-documentation.md#completion) と照合 | 未決・未検証事項が整理され、次の作業範囲が明確である |

### 補足事項
- **モック対象外・文書のみの変更**: モックの作成や切り替えを省略し、通常の実装・検証（または整合性確認）を行ったうえで順序6へ進みます。
- **記入欄 `{{...}}` の扱い**: 初期段階ですべて埋める必要はありません。未定事項は推測で埋めず `PLAN.md` で管理します。
- **反復開発**: 最初の機能の要件と受け入れ条件が確定した時点でセットアップは完了です。以降は機能単位で順序3〜6を反復します。

## 4. 既存プロジェクトへの導入

既存の構成や要件に合わせて、必要な要素を段階的に取り込みます。

1. **正本の対応付け**: 既存文書の役割を確認し、テンプレートの各責務に対応付けます（既存文書がある場合、`docs/project.md` への転記は不要です）。
2. **標準の差分管理**: 既存の規約と競合する場合は理由と影響を確認し、合意済みの差分のみを取り込みます。同名ファイルは上書きせず、別名配置などで参照先を調整します。
3. **文書方針への記録**: `docs/document-policy.md` に採用した版や適用日、差分を記録します。テンプレートの構成に全面的に合わせる必要はありません。
4. **段階的な適用**: 直近で変更する小さな機能から順序3〜6を適用します。既存機能の遡及モック化や不要なリファクタリングは行いません。

## 5. 配布版の更新取り込み

本テンプレートの新版を取り込む際は自動適用を避け、第7節の変更履歴で変更内容を確認したうえで必要な差分のみを反映します（手順は第4節と同様）。プロジェクト固有の仕様や検証結果を上書きしないよう注意し、反映後は `docs/document-policy.md` の採用版を更新します。

## 6. 導入後の確認事項

- 決定事項の正本が1箇所に定まり、リンクで参照できること。
- 不要な文書・階層・依存関係が増加していないこと。
- モック対象の機能において、シナリオ・再現手順・合意内容が整合していること。
- 実処理への接続が確認され、未検証の項目が明記されていること。
- 未決事項が残っている機能が誤って完了扱いになっていないこと。

## 7. 変更履歴

配布版は `template/` の内容が変わるたびに上がります。標準の版は、その標準自体に変更があった場合のみ上がります。

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

