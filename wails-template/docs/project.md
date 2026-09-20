# Wails Template のプロジェクト定義

## 1. 目的と範囲

Wails 補足で合意した構造を動作可能な実装として確認するための参照アプリです。メモの編集、CSV 一括取り込み、起動・終了、およびアプリ内更新機能を含みます。業務プロダクトや汎用 CRUD 基盤を作るものではありません。

## 2. 制約・受け入れ条件

Windows デスクトップを主対象とします（ブラウザ server build は検証専用）。Go 機能の再利用、下書きとキャッシュの分離、結果確定後の通知を確認します。各系列の UI 受け入れと Windows 実機検証が済むまで、テンプレートを動作保証済みとは扱いません。

サンプルの上限値はタイトル100文字、本文10,000文字、CSV 1 MiB・1,000行です。大量データ対策の共通基盤は含まず、新しい製品へこれらの上限を暗黙に引き継ぎません。

<a id="usecases"></a>
## 3. ユースケース一覧

| UC ID | 主アクター | 目的 | 実装順序 | 実現パターン | モック適用 |
| --- | --- | --- | --- | --- | --- |
| [UC-1](usecases/UC-1.md) | 利用者 | メモを作成・編集して保存する | 1 | [UCP-1](architecture.md#patterns) | 対象 |
| [UC-2](usecases/UC-2.md) | 利用者 | CSVの内容を確認して一括登録する | 2 | [UCP-2](architecture.md#patterns) | 対象 |
| [UC-3](usecases/UC-3.md) | 利用者 | 新版を確認してアプリを更新する | 3 | UCP-2の外部プロセス適用 | 対象 |

<a id="design"></a>
## 4. 確認した事実

参照実装の作成時に、配布元の版13・コミット `88b31b40c65a2ce35201eeda34358e2d103bac23` を確認しました。採用する共通資材は、生成に使う同一チェックアウトの `template/`（現在の配布版16）に従います。標準2文書と検査スクリプトは Git blob の一致を確認して複製します。

Wails 本体・CLI・npm ランタイムは `v3.0.0-beta.23` / `3.0.0-beta.23`、Go 1.25以上を前提とします。生成 API・Service 登録・ライフサイクル・runtime Vite plugin は [固定版ソース](https://github.com/wailsapp/wails/tree/v3.0.0-beta.23/v3) および [CLI資料](https://v3.wails.io/guides/cli/)（2026-09-20確認）に基づきます。

外部依存を取得できないため、npm 依存の解決・相互の型整合・生成バインディングの検証は未完了です。主要な仕様判断と、実際に実行できた検証を区別します。

<a id="commands"></a>
## 5. 実行・切り替え・検証手順

起動およびビルド手順は [README](../README.md) に集約しています。本リポジトリのルートで `mise run init:wails ../my-wails-app` を実行後、生成先を作業ディレクトリとします（`wails-template/` 単体を作業ディレクトリとしません）。`docs/project.md` と `docs/document-policy.md` は Wails 差分で個別管理し、生成時に共通版を全体上書きします。部分マージは行いません。`build/app.json` の変更後は再ビルドします。

`dev:mock` は Wails を起動したままメモ機能のみを固定データへ差し替えます（画面に「試験用モック」が表示されることを確認）。本番ビルドでモック設定を検出した場合はビルドを中止します。

E2E は実 Go サービスと専用の一時データ領域を使用します。`python scripts/doc_check.py .` では文書リンク・UC 対応・標準ハッシュ等を確認します。Windows 実機確認では、保存後の再起動、未保存状態からの終了、多重起動、CSV 処理中の終了、インストール・更新・アンインストール後のデータ保持を確認します。

<a id="release"></a>
### 更新元と署名

公開鍵と更新元は初回インストーラーに埋め込みます。開発者は秘密鍵をリポジトリ外で生成します。

```powershell
node scripts/run.mjs release keygen -out "$env:USERPROFILE/wails-release-private-key.txt"
```

表示された公開鍵を `build/app.json` の `updatePublicKey` に設定します。`updateSource` には共有フォルダの絶対パス、または公開 GitHub Releases の `https://github.com/OWNER/REPO/releases/latest/download/update.json` を設定します。JSON で Windows パスを記述する場合はバックスラッシュをエスケープします。private リポジトリの認証は対象外です。

初回版を `package` で作成します。新版発行時は `build/app.json` の版だけを増やし、同じ ID・公開鍵・更新元で `package` を実行します。

```powershell
node scripts/run.mjs package
node scripts/run.mjs release manifest -key "$env:USERPROFILE/wails-release-private-key.txt" -installer bin/wails-template-0.2.0-amd64-setup.exe -app-id io.github.nuitsjp.wails-template -version 0.2.0 -arch amd64 -out bin -notes "更新内容"
```

生成した `update.json` と対象インストーラーを同じ共有フォルダまたは Release assets へ配置します。共有フォルダではインストーラーを先に完全配置し、更新情報を最後に置き換えます。GitHub Releases は Draft へ両方を配置してから公開します。ZIP や別形式を `setup.exe` として指定しません。

旧版の更新画面で確認・取得・適用します。NSIS の実行待ちは旧アプリ PID を対象とします。署名・ハッシュ違反、適用失敗、組織ポリシーの拒否を成功扱いにしません。復旧時は同じ信頼できるインストーラーを手動実行します。

**この参照版の制約:** Runtime がない端末は WebView2 を事前導入します。署名鍵の自動交換、private Releases、古いステージファイルの自動清掃、インストーラーのトランザクション復旧は実装していません。インストーラー自体は未署名であり、実行許可は環境に依存します。

<a id="verification"></a>
## 6. 検証結果

完了報告では `node scripts/run.mjs verify` の結果と、Windows 実機確認の範囲を分けて記録します。Wails server build・Playwright・Go テストの結果は、Windows 実機の起動・インストール・更新確認の代わりにしません。

| UC・系列 ID | 構成 | 実行日 | コマンド | 合否 | 対象コミットまたは CI 参照 |
| --- | --- | --- | --- | --- | --- |
| UC-1-M / UC-1-X1 | Wails server + Playwright | — | `node scripts/run.mjs verify` | 未検証 | — |
| UC-2-M | Wails server + Playwright | — | `node scripts/run.mjs verify` | 未検証 | — |
| UC-3-M | Windows + NSIS | — | READMEと本節の更新手順 | 未検証 | — |

作成環境での限定検証（2026-09-20）: Go の `internal/`・`cmd/` を変更せず、一時 Go 1.23 モジュールへコピーし、`GOTOOLCHAIN=local GOPROXY=off go test -race ./internal/... ./cmd/...` を実行して合格しました。公開 Go モジュールは Wails のため1.25以上のままです。この結果は機能単位であり、Wails ブリッジや画面の検証ではありません。

全体検証を妨げた事実: GitHub・proxy.golang.org・registry.npmjs.org を提供環境から DNS 解決できず、Go ツールチェーン・Wails・npm 依存を取得できませんでした。Windows 環境と NSIS もないため、全体ビルド・ネイティブ起動・更新は未検証です。生成物の代わりに手書きバインディングや架空の成功結果は置いていません。

追加の限定検証（2026-09-20）: Go テスト21本（サブケースを除く）・race 検査・go vet に合格しました。独立した Go 機能群とリリース CLI は Windows amd64/arm64 向けクロスビルドに合格しました。TypeScript/TSX 30ファイルの構文・相対参照、JSON/YAML/XML の構文、文書検査（NG 0件）も確認しました。TypeScript の依存型検査、Wails バインディング生成、Windows 実行の代用にはしていません。
