# NPolyglot.Parsers.Sprache [![NuGet](https://img.shields.io/nuget/v/NPolyglot.Parsers.Sprache.svg)](https://www.nuget.org/packages/NPolyglot.Parsers.Sprache)

A package adding the abbility to use [Sprache](https://github.com/sprache/Sprache) parsers with NPolyglot.

To add a Sprache parser to your project:
* add a class
* add a property returning the parser's name:

  ```cs
  public static string ExportName
  {
      get
      {
          return "MyParser";
      }
  }
  ```
  This is what will be used as parser identifier for `DslCode` files (e.g. `@parser MyParser`)

* add a method that parses input:

  ```cs
  public static object ParseString(string input)
  {
      return new { Input = intput };
  }
  ```

  This is where you will pass the input to yout root Sprache parser.
* set the class file's build action to SpracheParser (either in Visual Studio or in csproj)

With that you can start writing your Sprache parser - the Sprache library will be provided for that class during compilation, so you don't have to reference in yourself (but you might want to to get autocompletion to work).

A simple parser would look like this:

```cs
using System.Collections.Generic;
using System.Linq;

using Sprache;

namespace DslParsers
{
    public static class IntListParser
    {
        public static string ExportName => "IntList";
        public static object ParseString(string input) => IntList.Parse(input.Trim());

        private static Parser<string> NegativeSwitch => Parse.String("-").Text();
        private static Parser<string> Delimiter => Parse.String(";").Text();

        private static Parser<int> Int =>
            from neg in NegativeSwitch.Optional()
            from numStr in Parse.Digit.Many().Text()
            let factor = neg.IsDefined ? -1 : 1
            let num = int.Parse(numStr)
            select factor * num;

        private static Parser<List<int>> IntList =>
            from ints in Int.Token().DelimitedBy(Delimiter)
            select ints.ToList();
    }
}
```