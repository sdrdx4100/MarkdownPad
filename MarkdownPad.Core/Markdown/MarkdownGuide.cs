namespace MarkdownPad.Core.Markdown;

/// <summary>
/// The cheat sheet shown by Help > マークダウンガイド.
/// </summary>
/// <remarks>
/// It used to be assigned straight over the editor's contents, which silently
/// destroyed unsaved work. It is now presented in its own window, with an
/// explicit "insert at caret" action.
/// </remarks>
public static class MarkdownGuide
{
    public const string Text = """
        # マークダウンガイド

        ## 見出し
        # 見出し1
        ## 見出し2
        ### 見出し3

        ## テキスト装飾
        **太字**  __太字__
        *斜体*  _斜体_
        ~~取り消し線~~
        `インラインコード`

        ## リスト
        - 箇条書き
        - 箇条書き
          - ネスト

        1. 番号付き
        2. 番号付き

        - [ ] 未完了のタスク
        - [x] 完了したタスク

        ## リンクと画像
        [リンクテキスト](https://example.com)
        ![画像の説明](images/screenshot.png)

        ## コードブロック
        ```csharp
        Console.WriteLine("Hello");
        ```

        ## 引用
        > 引用文
        > 複数行にできます

        ## 水平線
        ---

        ## テーブル
        | 左寄せ | 中央 | 右寄せ |
        |:-------|:----:|-------:|
        | A      | B    | C      |

        ## 脚注
        本文[^1]

        [^1]: 脚注の内容
        """;
}
