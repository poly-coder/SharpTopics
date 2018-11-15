namespace SharpFunky
open System
open System.Globalization
open System.Text
open System.Text.RegularExpressions

module String =
  let invariantCulture = CultureInfo.InvariantCulture
  let comparer = StringComparer.InvariantCulture
  let comparerIC = StringComparer.InvariantCultureIgnoreCase
  let comparerO = StringComparer.Ordinal
  let comparerOIC = StringComparer.OrdinalIgnoreCase
  let comparerC = StringComparer.CurrentCulture
  let comparerCIC = StringComparer.CurrentCultureIgnoreCase

  let isNullOrEmpty = System.String.IsNullOrEmpty
  let isNullOrWS = System.String.IsNullOrWhiteSpace
  let isNotNullOrEmpty = isNullOrEmpty >> not
  let isNotNullOrWS = isNullOrWS >> not

  let toBase64 bytes = bytes |> Convert.ToBase64String
  let fromBase64 str = str |> Convert.FromBase64String
  let fromBase64Opt = Option.catch fromBase64
  let fromBase64Res = Result.catch fromBase64

  let toEncoding (encoding: Encoding) (str: string) = str |> encoding.GetBytes
  let fromEncoding (encoding: Encoding) bytes = bytes |> encoding.GetString

  let toUtf8 = toEncoding Encoding.UTF8
  let fromUtf8 = fromEncoding Encoding.UTF8
  let toUtf7 = toEncoding Encoding.UTF7
  let fromUtf7 = fromEncoding Encoding.UTF7
  let toUtf32 = toEncoding Encoding.UTF32
  let fromUtf32 = fromEncoding Encoding.UTF32
  let toUnicode = toEncoding Encoding.Unicode
  let fromUnicode = fromEncoding Encoding.Unicode
  let toAscii = toEncoding Encoding.ASCII
  let fromAscii = fromEncoding Encoding.ASCII

  let isMatch regex =
    let expression = Regex(regex)
    fun (s: string) -> Prelude.isNotNull s && expression.IsMatch(s)

module Byte =
  let minValue = System.Byte.MinValue
  let maxValue = System.Byte.MaxValue
  let toString (b: byte) = b.ToString()
  let parseWith styles provider str =
    System.Byte.Parse(str, styles, provider)
  let tryParseWith styles provider str =
    System.Byte.TryParse(str, styles, provider) |> Option.ofTryOp
  let parse str =
    System.Byte.Parse(str, String.invariantCulture)
  let tryParse str =
    System.Byte.TryParse(str, NumberStyles.Integer, String.invariantCulture) |> Option.ofTryOp

module Int16 =
  let minValue = System.Int16.MinValue
  let maxValue = System.Int16.MaxValue
  let toString (b: int16) = b.ToString()
  let parseWith styles provider str =
    System.Int16.Parse(str, styles, provider)
  let tryParseWith styles provider str =
    System.Int16.TryParse(str, styles, provider) |> Option.ofTryOp
  let parse str =
    System.Int16.Parse(str, String.invariantCulture)
  let tryParse str =
    System.Int16.TryParse(str, NumberStyles.Integer, String.invariantCulture) |> Option.ofTryOp

module Int32 =
  let minValue = System.Int32.MinValue
  let maxValue = System.Int32.MaxValue
  let toString (b: int) = b.ToString()
  let parseWith styles provider str =
    System.Int32.Parse(str, styles, provider)
  let tryParseWith styles provider str =
    System.Int32.TryParse(str, styles, provider) |> Option.ofTryOp
  let parse str =
    System.Int32.Parse(str, String.invariantCulture)
  let tryParse str =
    System.Int32.TryParse(str, NumberStyles.Integer, String.invariantCulture) |> Option.ofTryOp

module Int64 =
  let minValue = System.Int64.MinValue
  let maxValue = System.Int64.MaxValue
  let toString (b: int64) = b.ToString()
  let parseWith styles provider str =
    System.Int64.Parse(str, styles, provider)
  let tryParseWith styles provider str =
    System.Int64.TryParse(str, styles, provider) |> Option.ofTryOp
  let parse str =
    System.Int64.Parse(str, String.invariantCulture)
  let tryParse str =
    System.Int64.TryParse(str, NumberStyles.Integer, String.invariantCulture) |> Option.ofTryOp

module Single =
  let minValue = System.Single.MinValue
  let maxValue = System.Single.MaxValue
  let toString (b: float32) = b.ToString()
  let parseWith styles provider str =
    System.Single.Parse(str, styles, provider)
  let tryParseWith styles provider str =
    System.Single.TryParse(str, styles, provider) |> Option.ofTryOp
  let parse str =
    System.Single.Parse(str, String.invariantCulture)
  let tryParse str =
    System.Single.TryParse(str, NumberStyles.Integer, String.invariantCulture) |> Option.ofTryOp

module Double =
  let minValue = System.Double.MinValue
  let maxValue = System.Double.MaxValue
  let toString (b: float) = b.ToString()
  let parseWith styles provider str =
    System.Double.Parse(str, styles, provider)
  let tryParseWith styles provider str =
    System.Double.TryParse(str, styles, provider) |> Option.ofTryOp
  let parse str =
    System.Double.Parse(str, String.invariantCulture)
  let tryParse str =
    System.Double.TryParse(str, NumberStyles.Integer, String.invariantCulture) |> Option.ofTryOp

module Decimal =
  let minValue = System.Decimal.MinValue
  let maxValue = System.Decimal.MaxValue
  let toString (b: decimal) = b.ToString()
  let parseWith styles provider str =
    System.Decimal.Parse(str, styles, provider)
  let tryParseWith styles provider str =
    System.Decimal.TryParse(str, styles, provider) |> Option.ofTryOp
  let parse str =
    System.Decimal.Parse(str, String.invariantCulture)
  let tryParse str =
    System.Decimal.TryParse(str, NumberStyles.Integer, String.invariantCulture) |> Option.ofTryOp

module Disposable =
    open System.Threading

    let dispose d = disposeOf d

    let createInstance fn =
        { new IDisposable with
            member __.Dispose() = fn() }
    
    let create fn =
        let disposed = ref false
        let dispose () = if not !disposed then disposed := true; fn()
        createInstance dispose
    
    let createThreadSafe fn =
        let disposed = ref 0
        let dispose() = if Interlocked.CompareExchange(disposed, 1, 0) = 0 then fn()
        createInstance dispose

    let noop = createInstance ignore
