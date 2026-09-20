# Wails Template のプロジェクト定義

## 1. 目的と範囲

Wails補足で合意した構造を、動作可能な実装として読むための参照アプリである。メモの編集、一括取り込み、起動・終了と更新機能を含む。業務プロダクトや汎用CRUD基盤を作るものではない。

## 2. 制約・受け入れ条件

Windowsデスクトップを対象とする。ブラウザserver buildは検証専用。Go機能の再利用、下書きと取得キャッシュの分離、結果確定後の通知を確認する。各系列のUI受け入れ・Windows実機検証が済むまでテンプレートの動作保証済みとはしない。

サンプルの上限はタイトル100文字、本文10,000文字、CSV 1 MiB・1,000行。大量データ対策の共通基盤は含まない。新しい製品へこれらの上限を暗黙に引き継がない。

<a id="usecases"></a>
## 3. ユースケース一覧

| UC ID | 主アクター | 目的 | 実装順序 | 実現パターン | モック適用 |
| --- | --- | --- | --- | --- | --- |
| [UC-1](usecases/UC-1.md) | 利用者 | メモを作成・編集して保存する | 1 | [UCP-1](architecture.md#patterns) | 対象 |
| [UC-2](usecases/UC-2.md) | 利用者 | CSVの内容を確認して一括登録する | 2 | [UCP-2](architecture.md#patterns) | 対象 |
| [UC-3](usecases/UC-3.md) | 利用者 | 新版を確認してアプリを更新する | 3 | UCP-2の外部プロセス適用 | 対象 |

<a id="design"></a>
## 4. 確認した事実

参照実装の作成時に、配布元の版13・コミット `88b31b40c65a2ce35201eeda34358e2d103bac23` を確認した。採用する共通資材の版は、生成に使った同一チェックアウトの `template/` に従う。標準2文書と検査スクリプトはGit blobの一致を確認して複製した。

Wails本体・CLI・npmランタイムは `v3.0.0-beta.23` / `3.0.0-beta.23` に合わせる。Go 1.25以上を要求する。生成API・Service登録・ライフサイクル・runtime Vite pluginは [固定版ソース](https://github.com/wailsapp/wails/tree/v3.0.0-beta.23/v3) と [CLI資料](https://v3.wails.io/guides/cli/) を2026-09-20に確認した。

外部依存を取得できないため、npm依存の解決・相互の型整合、生成されたバインディングの検証は未完了である。主要な仕様判断と、実際に実行できた検証は区別する。

<a id="commands"></a>
## 5. 実行・切り替え・検証手順

起動とビルド入口は [README](../README.md) に集約する。生成前にリポジトリのルートで `mise run init:wails ../my-wails-app` を実行し、生成後はそのプロジェクトのルートを作業ディレクトリとする。`wails-template/` を単独の作業ディレクトリとして実行しない。`docs/project.md` と `docs/document-policy.md` はWails差分で個別管理する文書であり、生成時は差分側の内容で上書きし、共通版との部分マージは行わない。`build/app.json`の変更後は再ビルドする。`dev:mock`はWails本体を起動したままメモ機能だけを固定データへ差し替える。画面に「試験用モック」が表示されることを確認する。本番ビルドでモック設定を検出したら失敗する。

E2Eは実Goサービスと専用の一時データ領域を使う。`python scripts/doc_check.py .` は文書リンク・UC対応・標準ハッシュ等を確認する。Windows確認では、メモ保存後の再起動、未保存状態からの終了、多重起動、CSV処理中の終了、インストール・更新・アンインストール後のデータ保持を確認する。

<a id="release"></a>
### 更新元と署名

公開鍵と更新元は最初のインストーラーへ埋め込む。開発者は秘密鍵をリポジトリ外へ作成する。

```powershell
node scripts/run.mjs release keygen -out "$env:USERPROFILE/wails-release-private-key.txt"
```

表示された公開鍵を`build/app.json`の`updatePublicKey`へ設定する。`updateSource`には共有フォルダの絶対パス、または公開GitHub Releasesの `https://github.com/OWNER/REPO/releases/latest/download/update.json` を設定する。JSONでWindowsパスを記述する際はバックスラッシュをエスケープする。privateリポジトリの認証は対象外である。

初回版を`package`で作成する。新版では`build/app.json`の版だけを増やし、同じID・公開鍵・更新元で`package`を実行する。例として0.2.0/x64を発行する場合:

```powershell
node scripts/run.mjs package
node scripts/run.mjs release manifest -key "$env:USERPROFILE/wails-release-private-key.txt" -installer bin/wails-template-0.2.0-amd64-setup.exe -app-id io.github.nuitsjp.wails-template -version 0.2.0 -arch amd64 -out bin -notes "更新内容"
```

生成した`update.json`と対象インストーラーを同じ共有フォルダ／Release assetsへ配置する。共有フォルダではインストーラーを先に完全配置し、更新情報を最後に置き換える。GitHub ReleasesはDraftへ両方を配置してから公開する。ZIPや別形式を`setup.exe`として指定しない。

旧版の更新画面で確認→取得→適用する。NSISの実行待ちは旧アプリPIDを対象とする。署名・ハッシュ違反、適用失敗、組織ポリシーの拒否では成功扱いにしない。復旧時は同じ信頼できるインストーラーを手動実行する。

**この参照版の制約:** Runtimeがない端末はWebView2を事前導入する。署名鍵の自動交換、private Releases、古いステージファイルの自動清掃、インストーラーのトランザクション復旧は実装していない。インストーラー自体は未署名であり実行許可は環境依存である。

<a id="verification"></a>
## 6. 検証結果

完了報告では `node scripts/run.mjs verify` の結果と、Windows実機で確認した範囲を分けて記録する。Wails server build・Playwright・Goテストの結果は、Windows実機の起動・インストール・更新確認の代わりにしない。

| UC・系列 ID | 構成 | 実行日 | コマンド | 合否 | 対象コミットまたは CI 参照 |
| --- | --- | --- | --- | --- | --- |
| UC-1-M / UC-1-X1 | Wails server + Playwright | — | `node scripts/run.mjs verify` | 未検証 | — |
| UC-2-M | Wails server + Playwright | — | `node scripts/run.mjs verify` | 未検証 | — |
| UC-3-M | Windows + NSIS | — | READMEと本節の更新手順 | 未検証 | — |

作成環境での限定検証（2026-09-20）: Goの`internal/`・`cmd/`を変更せず、一時Go 1.23モジュールへコピーし `GOTOOLCHAIN=local GOPROXY=off go test -race ./internal/... ./cmd/...` を実行、合格。公開GoモジュールはWailsのため1.25以上のままとした。対象は機能単位でありWailsブリッジ・画面の検証ではない。

全体検証を妨げた事実: GitHub・proxy.golang.org・registry.npmjs.orgへ提供環境からDNS解決できず、Goツールチェーン・Wails・npm依存を取得できなかった。Windows環境・NSISもないため、全体ビルド・ネイティブ起動・更新を未検証とする。生成物の代わりに手書きバインディングや架空の成功結果を置いていない。

追加の限定検証（2026-09-20）: Goテスト21本（サブケースを除く）・race検査・go vetに合格。独立したGo機能群とリリースCLIはWindows amd64/arm64向けクロスビルドに合格。TypeScript/TSX 30ファイルの構文・相対参照、JSON/YAML/XMLの構文、文書検査（NG 0件）も確認した。TypeScriptの依存型検査、Wailsバインディング生成、Windows実行の代用にはしていない。
