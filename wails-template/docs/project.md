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
| [UC-3](usecases/UC-3.md) | 利用者 | 新版を確認してアプリを更新する | 3 | [UCP-3](architecture.md#patterns) | 対象 |

<a id="design"></a>
## 4. 確認した事実

参照実装の作成時に、配布元の版13・コミット `88b31b40c65a2ce35201eeda34358e2d103bac23` を確認しました。採用する共通資材は、生成に使う同一チェックアウトの `template/`（現在の配布版18）に従います。標準2文書と検査スクリプトは Git blob の一致を確認して複製します。

Wails 本体・CLI・npm ランタイムは `v3.0.0-beta.23` / `3.0.0-beta.23`、Go 1.25以上を前提とします。生成 API・Service 登録・ライフサイクル・runtime Vite plugin は [固定版ソース](https://github.com/wailsapp/wails/tree/v3.0.0-beta.23/v3) および [CLI資料](https://v3.wails.io/guides/cli/)（2026-09-20確認）に基づきます。

生成先で依存取得、Wailsバインディング生成、TypeScript型検査、実Goサービスを使うE2Eを確認しました。主要な仕様判断と、実際に実行できた検証を区別します（[検証結果](#verification) 参照）。

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

| UC・系列 ID | 段階 | 構成 | 実行日 | コマンド | 合否 | 対象コミットまたは CI 参照 |
| --- | --- | --- | --- | --- | --- | --- |
| UC-1-M / UC-1-X1 | — | Wails server + Playwright | — | `node scripts/run.mjs verify` | 未検証 | — |
| UC-2-M | — | Wails server + Playwright | — | `node scripts/run.mjs verify` | 未検証 | — |
| UC-3-M | — | Windows + NSIS | — | READMEと本節の更新手順 | 未検証 | — |

合否を記入するときは [モック標準](standards/mock-driven-development.md#workflow) の段階番号（1〜6）も記録します。段階6の結果には対象系列の合意記録と完成系監査記録が必要であり、一部構成の合格のみで完了とは扱いません。

参照実装の保守検証（2026-09-21、Wails拡張0.2.0、Windows 11 Enterprise 24H2 amd64、Go 1.26.5・Node.js 24.16.0・Python 3.14・NSIS 3.12・WebView2 153）: 生成先で `node scripts/run.mjs verify`・`build`・`package` に合格しました。型検査・Lint・Vitest 7件・Go全パッケージテスト・go vet（desktop・server 両構成）・文書検査（NG 0件）・server build・Windows向け本番ビルド・NSISインストーラー作成を確認しました。実Goサービスのserver E2E 3件は、この環境では Playwright の Chromium 取得がプロキシで完了せず未実行です（2026-09-20 の保守検証で合格した記録を最後の結果とします）。

Windows 実機では、NSIS による版 0.1.0 のサイレントインストール（配置・レジストリ・スタートメニュー）、WebView2 での起動、多重起動時の2つ目のプロセス終了、ウィンドウ閉鎖時の終了確認（取り消しで継続、確認で終了）、共有フォルダを更新元とした版 0.2.0 への更新（署名済み更新情報の受理、取得とハッシュ検証、NSIS への引き渡し、旧プロセス終了待ち、適用後の新版起動、レジストリの版更新）、サイレントアンインストール後の登録情報削除とユーザーデータ保持を UI Automation による操作で確認しました。

実機では未確認の項目: 画面へのキー入力を伴う保存・取り込み（保存・下書き保持・一括取り込みは server E2E で確認）、CSV 処理中の終了、未署名実行制御（SmartScreen 等）。上表は系列ごとの利用者合意と完成系監査を経た検証用であり、この保守検証の合格を段階6完了や製品の合意記録には転用しません。
