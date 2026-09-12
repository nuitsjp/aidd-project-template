# AIDD Project Template

配布版: **2**

過剰なドキュメント作成や設計（オーバーエンジニアリング）を抑え、動作するモックで仕様を合意してから実処理へ接続する「モック駆動開発」のテンプレートです。

言語やフレームワーク、開発ツールは固定しません。本リポジトリから [template/](template/) の内容と [LICENSE](LICENSE) をコピーし、プロジェクトの要件に合わせて利用します。

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

最初はプロジェクト固有の内容を `docs/project.md` に集約します。設計書や用語集などの個別文書は、分割管理が必要になった時点で作成してください（空の文書を一式そろえる必要はありません）。

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

コピー後の文書はプロジェクト内のみを参照し、配布元との同期設定は不要です。

AIエージェントを利用する場合は、コピー先の `AGENTS.md` が読み込まれることを確認します。ツール固有の設定が必要な場合は、共通規則を複製せず参照してください。例えば Claude Code の場合は、コピー先に `@AGENTS.md` と記載した `CLAUDE.md` を作成します（詳細は [公式ドキュメント](https://code.claude.com/docs/en/memory) を参照）。

```markdown
@AGENTS.md
```

## 3. 初期セットアップと最初の機能実装

以下の手順で、最初の小さな機能を実処理まで実装します。全機能の設計やモック作成を先行させる必要はありません。

| 順序 | 作業内容 | 完了条件 |
| --- | --- | --- |
| 1 | `docs/document-policy.md` に採用記録、モック適用範囲、差分を記入 | 採用規則と範囲が確定し、状態を「適用済み」に更新 |
| 2 | `README.md`（概要）と `docs/project.md` 第1・2節（目的、対象、制約、受け入れ条件）を記入 | 解決する問題と対象外の範囲を説明できる |
| 3 | `docs/project.md` 第3・4節に最初の機能のシナリオ、外部仕様、責務と境界を記入 | 事実と仮定が区別され、期待結果の検証方法が決まっている |
| 4 | 対象機能に [モック駆動開発](template/docs/standards/mock-driven-development.md#workflow) を適用し、再現手順を `docs/project.md` 第5節へ記入 | 動作する状態で提示し、第3節に仕様合意を記録している |
| 5 | 合意した機能を実装して実処理へ接続。手順を第5節、結果や参照を第6節へ記入 | 検証に合格し、モックを無効化した環境でも期待通り動作する |
| 6 | `PLAN.md` を更新し、差分を [完了基準](template/docs/standards/design-and-documentation.md#completion) と照合 | 未決・未検証事項が整理され、次の作業範囲が明確 |

### 補足事項
- **モック対象外・文書のみの変更**: モック作成や切り替えを省き、通常の実装・検証、または整合性確認を行って順序6へ進みます。
- **記入欄 `{{...}}` の扱い**: すべての欄を初期段階で埋める必要はありません。未定事項は推測で埋めず `PLAN.md` で管理します。「未実装」「未検証」と記入した状態は完了扱いにはなりません。
- **導入の完了と反復**: 最初の機能の要件と受け入れ条件が定まった時点で初期セットアップは完了です。以降は機能単位で順序3〜6を繰り返します。

## 4. 既存プロジェクトへの導入

一括コピーせず、既存の要件や構成に合わせて必要な内容を取り込みます。

1. **正本の対応付け**: 既存の仕様書等の役割を確認し、テンプレートの各責務をどこが担うか対応付けます（既存文書がある場合は `docs/project.md` への複製は不要です）。
2. **標準の差分管理**: 既存の合意を変更する場合は理由と影響を確認し、合意済みの範囲のみを取り込みます。同名ファイルがある場合は上書きせず、別名配置などで参照を合わせます。
3. **文書方針への記録**: `docs/document-policy.md` に標準の版や適用日、差分を記録し、リンクを更新します。テンプレートの構成へ全面的に移行する必要はありません。
4. **段階的な適用**: 次に変更する小さな機能から順序3〜6を適用します。既存全機能のモック化や無関係なリファクタリングは不要です。

## 5. 配布版の更新取り込み

本テンプレートの新版を自動適用せず、既存の変更点と比較して必要な差分のみを取り込みます（手順は第4節と同様）。プロジェクト固有の要件や設計、検証結果をテンプレートで上書きしないでください。取り込み後は `docs/document-policy.md` の採用版を更新します。

## 6. 導入後の確認事項

- 各決定事項の正本が1箇所に定まり、リンクで辿れること。
- 不要な階層・依存関係・文書を増やしていないこと。
- モック機能において、シナリオ・再現手順・合意内容が対応していること。
- 実処理への切り替えが確認され、未検証の範囲が明記されていること。
- 未決事項が残っている機能を誤って完了扱いにしていないこと。
