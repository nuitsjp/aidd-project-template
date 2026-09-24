# AIDD Project Template

過剰な設計や文書作成を抑え、動作するモックで仕様合意を形成してから実処理へ接続する「モック駆動開発」のテンプレートです。

実装の言語・フレームワークは特定せず、利用者へ確認を依頼する前の事前確認は依頼手順の自動実行で行います（Web UI では Playwright CLI）。本リポジトリから [template/](template/) の内容と [LICENSE](LICENSE) をコピーして利用します。

## 1. 配布物と役割

| ファイル | 役割 |
| --- | --- |
| [README.md](template/README.md) | プロジェクトの概要と実行手順への案内 |
| [AGENTS.md](template/AGENTS.md) | AIエージェントの行動規範（現在地、停止点、文書管理、完了報告） |
| [docs/document-policy.md](template/docs/document-policy.md) | 適用する標準、モック適用範囲、正本の配置、仕様変更の対象 |
| [docs/project.md](template/docs/project.md) | 目的・制約、ユースケース一覧、確認した事実、実行・検証手順 |
| [.agents/skills/usecase-docs/](template/.agents/skills/usecase-docs/SKILL.md) | ユースケース・シナリオの文書構造、作成手順と雛形。実仕様は `docs/usecases/<名称>/README.md` とその `scenarios/<名称>.md` に作成 |
| [docs/architecture.md](template/docs/architecture.md) | システム構成、共通方針、実現パターン一覧、設計上の制約 |
| [docs/design/UCP-1.md](template/docs/design/UCP-1.md) | 実現パターンごとの具体的な処理・役割・境界 |
| [docs/design/data.md](template/docs/design/data.md) | 保存形式と現在のテーブル設計 |
| [docs/standards/design-and-documentation.md](template/docs/standards/design-and-documentation.md) | 設計と文書化の基準、先行成果物の範囲、完了基準、変更手続き |
| [docs/standards/mock-driven-development.md](template/docs/standards/mock-driven-development.md) | 系列ごとの段階とゲート条件、仕掛かり上限、モックの境界 |
| [scripts/doc_check.py](template/scripts/doc_check.py) | 文書整合の判定6件（リンク、絶対パス、禁止記録・重複本文、ユースケース・シナリオ構造、標準ハッシュ、証跡・行数報告）。Python 3 標準ライブラリのみ |

正本の役割は [文書方針](template/docs/document-policy.md#sources) で定め、他の箇所には参照を置きます。現在の仕様・制約・手順のみを維持し、決定経緯・承認履歴・検証実績は配布文書へ保存しません。`docs/standards/` は配布元からの輸入物として編集せず、固有差分は `docs/document-policy.md` 第1節に記載します。

`scripts/doc_check.py` の実行には Python 3 が必要です（Python を使わない構成では導入を見送り、その旨を `docs/document-policy.md` 第1節の差分欄に記録します）。本テンプレートは [MIT ライセンス](LICENSE) で配布しているため、コピー時も著作権表示と許諾文を保持してください。

## 2. 新規プロジェクトへの導入

本リポジトリを取得し、`template/` の内容と `LICENSE` を新プロジェクトのルートへコピーします。正式に採用する際は、変更のない固定コミットのチェックアウトを使い、配布元で `git rev-parse HEAD` が返す40桁のSHAを `docs/document-policy.md` の採用元固定コミット欄に記録します。未コミットの保守変更からの生成は検証用とし、公開済みの採用コミットと混同しません。

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

コピー後に `python scripts/doc_check.py` を実行し、リンクと文書構造の整合性を確認します。隠しディレクトリ `.agents/skills/usecase-docs/` もコピー対象です。共通版のユースケースは未着手の一覧のみで、本文は合意後に同梱雛形から作成します（雛形は実仕様の検査対象外）。採用時の記入は第3節から始めます。共通版と各拡張の README にも、この開始手順への案内を残しています。

AIエージェントを利用する場合は、プロジェクト側の `AGENTS.md` が読み込まれるよう設定します。ツール固有の設定が必要な場合も規則を複製せず参照にとどめます。例えば Claude Code では、ルートに `@AGENTS.md` と記載した `CLAUDE.md` を配置します（詳細は [公式ドキュメント](https://code.claude.com/docs/en/memory) を参照）。

```markdown
@AGENTS.md
```

### Wailsアプリの初期状態を生成する

Windows向けのGo・React参照実装は [wails-template/reference/](wails-template/reference/README.md) にまとめています。miseとNode.js 22.16以上を用意し、本リポジトリのルートで実行します。設定の信頼確認を求められた場合は、`mise.toml` の内容を確認して `mise trust` を実行してください。

```powershell
mise run init:wails ../my-wails-app
cd ../my-wails-app
```

生成は `template/` → `wails-template/` の順にコピーし、ルートの `LICENSE` を配置したうえで、参照アプリの `docs/` と `README.md` を除く一式を製品ルートへコピーします。サンプルは生成先の `reference/` に元の内容で残します。ルートの `docs/` は製品の現行仕様を記述する場所で、`template/` の共通版から始まります。参照実装のユースケース・シナリオは `reference/docs/` にあります。出力先の親ディレクトリは事前に用意し、既存の出力先は指定しないでください。生成タスクは依存取得やビルドを行いません。`wails-template/` 単体をコピー・実行せず、生成先で開発してください。共通側の変更を取り込んだ初期状態は新しい出力先へ再生成して確認し、既存プロジェクトへの反映は第5節に従って差分を確認します。必要な環境と生成後のセットアップ・起動は [Wailsの実行手順](wails-template/reference/docs/project.md#commands) に従い、製品ルートと `reference/` のそれぞれで実行します。

### Reactアプリの初期状態を生成する

React・Node.js・SQLiteの参照実装は [react-template/reference/](react-template/reference/README.md) にまとめています。Wailsと同じ前提で、本リポジトリのルートから実行します。

```powershell
mise run init:react ../my-react-app
cd ../my-react-app
```

配置の規則はWailsと同様です。参照アプリを製品ルートへコピーし、サンプルを `reference/` に残します。生成タスクは依存取得やビルドを行わず、既存の出力先は拒否します。`react-template/` 単体をコピー・実行せず、生成先で開発してください。必要な環境と生成後のセットアップ・起動は [Reactの実行手順](react-template/reference/docs/project.md#commands) に従い、製品ルートと `reference/` のそれぞれで実行します。

### React・.NETの初期状態を生成する

React・ASP.NET Core・SQLiteの実行可能なサンプルは [react-dotnet-template/reference/](react-dotnet-template/reference/README.md) にまとめています。生成時にそのアプリ一式を製品ルートへコピーし、製品側の C# 名前空間、プロジェクト名、アセンブリ名と npm パッケージ名を指定した製品名に合わせます。サンプルは生成先の `reference/` に元の名前と内容で残します。ルートの `docs/` は製品の現行仕様を記述する場所で、メモのユースケース・シナリオは `reference/docs/` にあります。

```powershell
mise run init:react-dotnet ../my-react-dotnet-app --name Company.Product
cd ../my-react-dotnet-app
mise trust
mise run setup
mise run setup:browser
mise run verify
cd reference
mise trust
mise run setup
mise run setup:browser
mise run verify
```

`template/`、`react-dotnet-template/`、ルートの `LICENSE` を配置し、参照アプリから `backend/`、`frontend/`、`contracts/`、`tests/`、アプリの設定とスクリプトを製品ルートへコピーします。`--name` には `Company.Product` のような ASCII の C# 識別子をドットで区切って指定します。C# の予約語は使用できません。製品側には `<製品名>.slnx`、`backend/<製品名>.csproj`、`frontend/<製品名>.Frontend.esproj` と、`tests/dotnet-unit/<製品名>.UnitTests.csproj`、`tests/dotnet-integration/<製品名>.IntegrationTests.csproj` を配置します。生成先ルートと `reference/` にそれぞれ完全な `mise` アプリタスクがあり、`mise run dev` は Vite と .NET を起動し、`mise run build` と `mise run start` は UI を .NET に同梱します。Visual Studio では製品ルートの `<製品名>.slnx` を開き、`backend/<製品名>.csproj` をスタートアッププロジェクトにします。サンプルは `reference/App.slnx` から同様に起動できます。生成は依存取得やコード生成を行わず、既存の出力先は拒否します。

テンプレート開発時は `react-dotnet-template/` で製品文書、`react-dotnet-template/reference/` で参照アプリを検証します。source の文書検査は共通の `template/` と拡張側を一時生成先へ重ねます。製品側の実装・設定と依存ロックは生成後に採用先で管理します。生成・起動・検証の詳細は [参照実装の実行手順](react-dotnet-template/reference/docs/project.md#commands) に従います。

## 3. 初期セットアップと最初のユースケース

以下の順序で、最初のユースケース1件を実処理まで通します。作業単位・再開時の確認・停止点は [モック標準](template/docs/standards/mock-driven-development.md#workflow) に従います。

| 順序 | 作業内容 | 完了条件 |
| --- | --- | --- |
| 1 | `docs/document-policy.md` に採用元固定コミット、モック適用範囲の固有除外、差分を記入 | 採用規則と適用範囲が確定し、状態を「適用済み」に更新 |
| 2 | `README.md`（概要）、`docs/project.md` 第1・2節（目的、対象、制約、受け入れ条件）、第3節のカタログ表（ユースケース名、主アクター、目的、実装順序）の案をメッセージで議論し、利用者の確認後に記入 | 解決する問題と対象外を説明でき、最初のユースケースが決定 |
| 3 | アーキテクチャを決定づける外部依存の実測を行い、事実を `docs/project.md` 第4節と `docs/reference/` に記録 | 情報源・対象版・確認日が記録されている（該当する外部依存がなければ省略） |
| 4 | `docs/architecture.md` 第1・2節、パターンの名称と適用条件、第4節の設計上の制約を記入し、会話で全体設計の合意を得る | 利用者が対象範囲の全体設計を確認している |
| 5 | 最初のユースケースを [モック標準第2節](template/docs/standards/mock-driven-development.md#workflow) の段階1〜6で系列ごとに通す（主成功から着手し、拡張は1本ずつ追加。案の全文はメッセージで議論し、確認後に同梱雛形でユースケースとシナリオを別ファイルへ保存。テーブル追加・変更時は段階4開始前に設計合意） | 利用者が完成系を承認し、最新コードのテストと実環境検証が合格してモック標準の完了条件を満たす |
| 6 | 次のユースケースへ順序5を繰り返す（構造変更時は `docs/architecture.md`、処理・保存設計の変更時は対象の `docs/design/` 文書を参照） | 進行中の系列が常に1本以下 |

### 補足事項
- **利用者の動作確認**: モックの段階3と完成系監査の段階5で利用者に確認を依頼する前に、依頼する手順を自動実行して想定結果を確認します（Web UI では Playwright CLI）。依頼時は確認目的、簡潔な手順、期待結果、URL を提示します。
- **テストの追加・更新時点**: 新しい受け入れ条件の検証と、合意済みの仕様変更に伴う既存テストの維持を区別します。実施時点と本番実装の修正時に戻る段階は [モック標準第2節](template/docs/standards/mock-driven-development.md#workflow) に従います。
- **UI 確認が不要な系列・変更**: 段階2・3を省略し、段階1の確認後、必要なテーブル設計合意を得て段階4〜6（実装、完成系監査、E2E 検証）を実施します。文書のみの変更は `scripts/doc_check.py` で整合性を確認します。
- **テーブル設計の合意**: 現在のER図と定義を `docs/design/data.md`へ集約し、変更点を会話で提示して合意を得ます。設計に依存する保存処理やマイグレーションは合意後に実装します（設計変更がなければ再合意不要）。
- **記入欄 `{{...}}` の扱い**: 初期段階ですべて埋める必要はありません。未確定事項は推測で埋めず、停止点で利用者に確認します。

## 4. 既存プロジェクトへの導入

既存の構成や要件に合わせて、必要な要素を段階的に取り込みます。

1. **正本の対応付け**: 既存文書の役割を確認し、テンプレートの各責務に対応付けます（既存文書がある場合、`docs/project.md` への転記は不要）。
2. **標準の差分管理**: 既存の規約と競合する場合は理由と影響を確認し、合意済みの差分のみを `docs/document-policy.md` 第1節に記録します（`docs/standards/` 本文は改変しない）。
3. **文書方針への記載**: `docs/document-policy.md` に採用元固定コミットと差分を記載します（テンプレート構成への全面的な統一は不要）。
4. **段階的な適用**: 直近で変更する小さなユースケース1件から第3節の順序5を適用します。既存機能の遡及的なユースケース化やモック化、不要なリファクタリングは行いません。

## 5. 配布元の更新取り込み

配布元の更新前後のコミット間の差分とコミットメッセージで、採用する変更と必要な書式移行を確認し、次の単位で更新します。規則の採用判断と、ファイルの機械的な差し替えは分けます。

| 対象 | 更新方法 |
| --- | --- |
| `AGENTS.md`、`docs/standards/` の2標準、`scripts/doc_check.py`、`.agents/skills/usecase-docs/` のスキルと雛形2件 | 配布元が管理する7ファイル。同じ固定コミットから一組で差し替える。固有規則は `docs/document-policy.md` の差分欄で管理する。 |
| `README.md`、`docs/project.md`、`docs/architecture.md`、`docs/document-policy.md`、`docs/design/`・ユースケース／シナリオ本文・技術固有文書 | 初回生成後は採用先が管理する。雛形の全文は同期せず、コミット間の差分にある必須項目の移行だけを適用する。現在の仕様と検証手順を維持する。 |
| React/Wails/React・.NETの実装・設定・DB移行履歴 | 初回生成後は採用先が管理する。参照実装の差分は個別に評価する。現在、継続同期する共通コードパッケージは提供しない。 |
| 外部依存と生成コード | 各プロジェクトで依存定義とロックを更新し、既存の生成・検証コマンドを実行する。生成コードは手でマージしない。最低対応版と検証に使ったツール版は区別する。 |

共通7ファイルは、配布元リポジトリで次を実行して更新できます（Node.jsとGitが必要です）。`OLD_COMMIT_SHA` は文書方針の更新前コミット、`NEW_COMMIT_SHA` は採用する更新後コミットの40桁SHAに置き換え、両コミットを配布元のローカルGitで参照できる状態にします。

```sh
node scripts/update-common.mjs ../my-project OLD_COMMIT_SHA NEW_COMMIT_SHA
```

コマンドは既存資材が更新前の原本と一致することを確認してから差し替えます（LFとCRLFの差は許容）。更新前コミットに存在しない追加資材は、採用先にも同名ファイルがないことを確認して配置します。全7件を確認し、ローカル変更・既存資材の欠落・追加先の競合・不正なコミットがあれば書き込まず停止します。固有文書・コード・文書方針には触れません。採用元が不明な場合は履歴から特定し、原本との差分を確認してから利用します。変更済みファイルを強制上書きする機能はありません。

差し替え後は採用先で `python scripts/doc_check.py .` を実行し、必要な書式移行を完了してから `docs/document-policy.md` の採用元固定コミットを更新します。文書の見出し・表・記録欄が変更される場合は、その移行手順も必要です。競合しないことだけで仕様や合意の維持を保証したとは扱いません。

既存のADR・合意履歴にだけ残っている現行仕様は正本へ統合してから履歴を削除し、検証表・過去の実行結果・それらへの参照も削除します。

## 6. 導入後の確認事項

- `scripts/doc_check.py` の出力の NG を確認し、`docs/document-policy.md` 第1節に記録された合意済みの採用差分に該当するものを除いて解消していること。
- ユースケース案の全文をメッセージで議論し、利用者の確認後にファイルへ反映していること（会話と編集の順序は文書チェックスクリプトでは判定しません）。
- 情報ごとに正本が1箇所に定まり、他の箇所がリンクで参照していること。今回変更した事実について重複と古い説明を確認し、更新または削除していること。
- 不要な文書・階層・依存関係が増加していないこと。
- モック対象のユースケースにおいて、現在の系列・受け入れ条件・再現手順が整合していること。
- テーブル追加・変更時、依存する実装の前に `docs/design/data.md`で合意していること（合意の要否・内容は人が確認し、`scripts/doc_check.py` では判定しません）。
- 最新コードで必要なテストと実環境検証が合格していること。未実施の確認は会話で報告し、完了と扱わないこと。
- ADR・決定経緯・承認原文・検証表が残っていないこと。静的検査は既知の記録形式と完全一致する長い本文の重複を検出する。言い換えた重複・陳腐化・承認内容・実施順序・検証範囲はレビューで確認し、NG 0件だけで完了扱いにしないこと。
