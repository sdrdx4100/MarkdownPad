# MarkdownPad

マークダウン対応メモ帳アプリケーション (C# / WPF / .NET 8)

## 概要

MarkdownPad は、リアルタイムプレビューとスクリーンショット貼り付けに対応した
マークダウンエディターです。プレビューは WebView2 上で描画され、ローカル画像の
表示、スクロール位置の保持、エディターとの同期スクロールに対応しています。

## プロジェクト構成

| プロジェクト | ターゲット | 役割 |
|---|---|---|
| `MarkdownPad.Core` | `net8.0` | 文字コード判定・ファイル入出力・Markdown描画・書式変換などの UI 非依存ロジック |
| `MarkdownPad` | `net8.0-windows` | WPF アプリケーション本体（View / ViewModel / WebView2 連携） |
| `MarkdownPad.Tests` | `net8.0` | `MarkdownPad.Core` の xUnit テスト |

ロジックを WPF から切り離しているため、テストは Windows 以外でも実行できます。

## 機能

### マークダウン
- リアルタイムプレビュー（入力中もプレビューのスクロール位置を保持）
- エディターとプレビューのスクロール同期
- ローカル画像の表示（相対パス・絶対パスの両方）
- テーブル、タスクリスト、脚注などの拡張記法（Markdig の advanced extensions）
- 書式ツールバー。複数行を選択した状態での見出し・引用・リストの一括適用と解除
- プレビューのテーマ切り替え（ライト / ダーク / システム設定に従う）

### ファイル
- 保存は一時ファイル経由の原子的置換（保存中の異常終了で元ファイルを失いません）
- 他のプログラムによる変更を検知し、ウィンドウをアクティブにしたときに再読み込みを確認
- 文字コードの自動判別（UTF-8 / UTF-8 BOM / UTF-16 / Shift_JIS）と保持
- 改行コード（CRLF / LF / CR）の自動判別と保持
- 保存前の文字化け警告（選択中の文字コードで表現できない文字がある場合）
- 最近使ったファイル
- 自動バックアップとクラッシュ復旧
- HTML / PDF エクスポート、印刷

### 画像
- クリップボードからのスクリーンショット貼り付け（Ctrl+V / 右クリック / Shift+Insert）
- 画像ファイルのドラッグ＆ドロップと貼り付け
- 外部から取り込んだ画像は文書の `images/` にコピー（元ファイルを移動しても壊れません）
- 文書と同じ階層の `images/` への自動保存と、相対パスでの参照挿入

### エディター
- Enter でリスト・番号・引用・タスクを自動継続（空の項目で Enter を押すと解除）
- Enter でインデントを引き継ぎ
- Tab / Shift+Tab で選択行をまとめてインデント・アンインデント
- Alt+↑/↓ で行の移動、Ctrl+D で行の複製、Ctrl+Shift+K で行の削除
- 行番号表示、現在行のハイライト、選択文字数の表示
- 検索・置換（正規表現、前方/後方検索、折り返し、F3 で繰り返し）
  すべて置換は文書全体の 1 回の変更として適用され、Ctrl+Z 一回で戻せます
- ワードラップ切り替え、Ctrl+ホイール / Ctrl+± でフォントサイズ変更
- ウィンドウサイズや表示設定の保存

## 必要要件

- Windows 10 / 11
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)
  （Windows 11 および最近の Windows 10 には標準で同梱されています）
- 開発時: .NET 8 SDK

配布用の実行ファイルは自己完結型のため、.NET ランタイムのインストールは不要です。

## ビルドと実行

```bash
# リポジトリのルートで
dotnet restore MarkdownPad.sln
dotnet build MarkdownPad.sln -c Release
dotnet test MarkdownPad.Tests/MarkdownPad.Tests.csproj

# 実行（Windows）
dotnet run --project MarkdownPad/MarkdownPad.csproj
```

`MarkdownPad.Core` と `MarkdownPad.Tests` は Windows 以外でもビルド・実行できます。
WPF 本体は `EnableWindowsTargeting` により他 OS 上でもビルド検証だけは可能です。

## キーボードショートカット

| ショートカット | 機能 |
|---|---|
| Ctrl+N | 新規作成 |
| Ctrl+O | ファイルを開く |
| Ctrl+S | 保存 |
| Ctrl+Shift+S | 名前を付けて保存 |
| Ctrl+P | 印刷 |
| Ctrl+Z / Ctrl+Y | 元に戻す / やり直し |
| Ctrl+X / Ctrl+C / Ctrl+V | 切り取り / コピー / 貼り付け（画像対応） |
| Ctrl+A | すべて選択 |
| Ctrl+F / Ctrl+H | 検索 / 置換 |
| F3 / Shift+F3 | 次を検索 / 前を検索 |
| Ctrl+B / Ctrl+I | 太字 / 斜体 |
| Ctrl+K | リンクを挿入 |
| Tab / Shift+Tab | インデント / アンインデント |
| Alt+↑ / Alt+↓ | 行を上へ / 下へ移動 |
| Ctrl+D | 行を複製 |
| Ctrl+Shift+K | 行を削除 |
| Ctrl++ / Ctrl+- / Ctrl+0 | 拡大 / 縮小 / 標準のサイズ |
| Ctrl+ホイール | エディターの拡大・縮小 |

## データの保存場所

| 内容 | 場所 |
|---|---|
| 設定 | `%APPDATA%\MarkdownPad\settings.json` |
| WebView2 のユーザーデータ | `%LOCALAPPDATA%\MarkdownPad\WebView2` |
| プレビュー用アセット | `%LOCALAPPDATA%\MarkdownPad\preview` |
| クラッシュ復旧データ | `%LOCALAPPDATA%\MarkdownPad\backup` |
| 未保存時の画像の保存先 | `%USERPROFILE%\Documents\MarkdownPad\images` |

実行ファイルと同じ場所には何も書き込まないため、`Program Files` 配下に
インストールしても動作します。

## プレビューのセキュリティ

プレビューは Content-Security-Policy を適用した専用ページ上で描画されます。
文書に埋め込まれた `<script>` やインラインイベントハンドラーは実行されません。
さらに厳格にしたい場合は、[表示] > [MarkdownにHTMLを許可] をオフにすると、
生 HTML をエスケープして表示します。プレビュー内のリンクは既定のブラウザーで開きます。

## 使用ライブラリ

- [Markdig](https://github.com/xoofx/markdig) — マークダウンパーサー
- [Microsoft.Web.WebView2](https://learn.microsoft.com/microsoft-edge/webview2/) — プレビュー描画
- [xUnit](https://xunit.net/) — テスト

## リリース

`v*.*.*` 形式のタグを push すると、GitHub Actions が Windows 上でビルドとテストを
実行し、自己完結型の単一 EXE を ZIP と SHA256 チェックサム付きで Release に添付します。

## ライセンス

MIT License
