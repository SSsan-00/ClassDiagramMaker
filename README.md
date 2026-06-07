# ClassDiagramMaker

指定した C# のファイルやディレクトリを解析し、Mermaid のクラス図を生成するツールです。

GUI は Windows 向けの WinForms アプリケーションです。

## 必要環境

- .NET SDK 9.0
- WinForms GUI を実行する場合は Windows

このリポジトリには `global.json` が含まれているため、新しい SDK がインストールされている環境でも .NET 9 SDK を使用します。

## 実行方法

PowerShell では次のように実行します。

```powershell
dotnet restore src/ClassDiagramMaker/ClassDiagramMaker.csproj
dotnet run --project src/ClassDiagramMaker/ClassDiagramMaker.csproj
```

WinForms 画面で次の項目を指定します。

- 対象プロジェクトフォルダ
- 検索対象フォルダ
- 検索対象ファイル、任意
- 生成する `.mmd` ファイルの出力先

検索対象ファイルが空の場合は、検索対象フォルダ配下の `.cs`、`.cshtml.cs`、`.cshtml` ファイルを再帰的に解析します。GUI では解析中とレンダリング中の進捗を確認できます。

## 表示オプション

GUI では巨大なプロジェクトでも見やすくするため、出力内容を調整できます。

- 表示モード: 型だけ、主要メンバー、全メンバー
- 関係線: 継承、interface 実装、フィールド/プロパティ関連、メソッド依存を個別に切り替え
- 分割出力: namespace 単位またはフォルダ単位で Mermaid ファイルを分割し、任意で `index.md` と全体図を生成

## Razor 対応

Razor の `.cshtml` ファイルは Razor ページのノードとして表現されます。解析対象は次の要素です。

- `@model`
- `@inject`
- `@functions` / `@code` ブロックに定義されたメンバー
- マークアップ内の tag helper、view component、partial view 参照

`.cshtml.cs` の code-behind ファイルは通常の C# ソースとして解析します。

単一の Razor ファイルを指定した場合は、対応するペアも一緒に解析します。

- `Page.cshtml` を選択すると、存在する場合は `Page.cshtml.cs` も解析します。
- `Page.cshtml.cs` を選択すると、存在する場合は `Page.cshtml` も解析します。

## テスト

Core の解析処理は xUnit でテストしています。

```bash
dotnet test ClassDiagramMaker.sln
```

## リリース

PowerShell で Windows 用の単一 exe を生成できます。

```powershell
./tools/publish-single-exe.ps1
```

既定の出力先は次の通りです。

```text
artifacts/win-x64-single-file/ClassDiagramMaker.exe
```

起動に失敗した場合は、エラーダイアログを表示し、次のログファイルに詳細を書き込みます。

```text
%LOCALAPPDATA%\ClassDiagramMaker\ClassDiagramMaker.error.log
```

別の Windows runtime 向けに publish する場合は、`-Runtime` を指定します。

```powershell
./tools/publish-single-exe.ps1 -Runtime win-arm64
./tools/publish-single-exe.ps1 -Runtime win-x86
```

同等の `dotnet publish` コマンドは次の通りです。

```bash
dotnet publish src/ClassDiagramMaker/ClassDiagramMaker.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=none -p:DebugSymbols=false -p:CopyOutputSymbolsToPublishDirectory=false -o artifacts/win-x64-single-file
```

publish profile `win-x64-single-file` も利用できます。

```bash
dotnet publish src/ClassDiagramMaker/ClassDiagramMaker.csproj -p:PublishProfile=win-x64-single-file
```

## 分割出力

分割出力を有効にすると、指定した出力先のファイル名がプレフィックスとして使われます。
たとえば `diagram.mmd` を指定すると、次のようなファイルを生成できます。

```text
diagram.index.md
diagram.all.mmd
diagram.Demo.Services.mmd
diagram.Demo.Models.mmd
```

namespace 分割では C# の namespace ごとに型をまとめます。フォルダ分割では、対象プロジェクトフォルダから見たソースファイルの配置ごとに型をまとめます。

分割された図では、同じ分割ファイル内にある型同士の関係だけを出力します。任意で生成できる `*.all.mmd` には全体図を保持します。

## 解析できる依存関係

C# ソースは Roslyn の AST と SemanticModel を使って解析します。主な取得対象は次の通りです。

- 継承、interface 実装、フィールド、プロパティ、イベント、メソッド、コンストラクタ、インデクサ
- `abstract`、`sealed`、`static`、`readonly` などの修飾子
- generic 型引数、`where T : class` などの generic 制約
- 属性、`typeof(...)`、base 型の generic 引数
- メソッド本体内の `new`、cast、pattern matching、`var` 推論型、static メンバー呼び出し、generic メソッド型引数、呼び出し先の戻り値型
- `using static` と using alias 経由で参照した型
- delegate と class primary constructor

## 出力形式

最初に対応している出力形式は Mermaid の `classDiagram` です。

```mermaid
classDiagram
    direction LR
    class Repository_T {
        <<abstract>>
        #string CacheKey$
        +Create~TArg~(TArg arg) T*
    }
    note for Repository_T "where T : class, new()\\nCacheKey modifiers: static readonly\\nCreate constraints: where TArg : struct"
    class Pages_Users_Index {
        <<razor page>>
        +Demo.Pages.Users.IndexModel Model
        +Demo.Services.IUserRepository Repository
    }
    UserRepository <|.. UserService
    UserService --> UserRepository : repository
```

## Bootstrap

リポジトリをダウンロードできないユーザー向けに、単一ファイルの bootstrap スクリプトを用意しています。

Windows / PowerShell では次のコマンドを使います。

```powershell
powershell -ExecutionPolicy Bypass -File .\bootstrap\ClassDiagramMaker.bootstrap.ps1 .\ClassDiagramMaker
```

PowerShell の実行ポリシーでブロックされない環境では、次のように直接実行できます。

```powershell
.\bootstrap\ClassDiagramMaker.bootstrap.ps1 .\ClassDiagramMaker
```

macOS / Linux など `sh` が使える環境では、次のコマンドでも生成できます。

```bash
sh ./bootstrap/ClassDiagramMaker.bootstrap.sh ./ClassDiagramMaker
```

生成後、Windows では次のように起動または publish できます。

```powershell
cd .\ClassDiagramMaker
dotnet run --project .\src\ClassDiagramMaker\ClassDiagramMaker.csproj
.\tools\publish-single-exe.ps1
```

このスクリプトはアプリ本体と Core のソースツリーをローカルに再作成します。xUnit のテストコードは意図的に含めていません。

ソース変更後に bootstrap を再生成する場合は、次のコマンドを実行します。

```bash
./tools/generate-bootstrap.sh
```
