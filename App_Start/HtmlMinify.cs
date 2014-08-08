using System;
using System.Linq;
using System.Web;
using System.Text;
using System.IO;
using Microsoft.Ajax.Utilities;

namespace MvcMinifyHtml
{
    /// <summary>
    /// A semi-generic Stream implementation for Response.Filter with
    /// an event interface for handling Content transformations via
    /// Stream or String.   
    /// <remarks>
    /// Use with care for large output as this implementation copies
    /// the output into a memory stream and so increases memory usage.
    /// </remarks>
    /// </summary>   
    public class ResponseFilterStream : Stream
    {
        /// <summary>
        /// The original stream
        /// </summary>
        Stream _stream;

        /// <summary>
        /// Current position in the original stream
        /// </summary>
        long _position;

        /// <summary>
        /// Stream that original content is read into
        /// and then passed to TransformStream function
        /// </summary>
        MemoryStream _cacheStream = new MemoryStream(5000);

        /// <summary>
        /// Internal pointer that that keeps track of the size
        /// of the cacheStream
        /// </summary>
        int _cachePointer = 0;


        /// <summary>
        ///
        /// </summary>
        /// <param name="responseStream"></param>
        public ResponseFilterStream(Stream responseStream)
        {
            _stream = responseStream;
        }


        /// <summary>
        /// Determines whether the stream is captured
        /// </summary>
        private bool IsCaptured
        {
            get
            {

                if (CaptureStream != null || CaptureString != null ||
                    TransformStream != null || TransformString != null)
                    return true;

                return false;
            }
        }

        /// <summary>
        /// Determines whether the Write method is outputting data immediately
        /// or delaying output until Flush() is fired.
        /// </summary>
        private bool IsOutputDelayed
        {
            get
            {
                if (TransformStream != null || TransformString != null)
                    return true;

                return false;
            }
        }


        /// <summary>
        /// Event that captures Response output and makes it available
        /// as a MemoryStream instance. Output is captured but won't
        /// affect Response output.
        /// </summary>
        public event Action<MemoryStream> CaptureStream;

        /// <summary>
        /// Event that captures Response output and makes it available
        /// as a string. Output is captured but won't affect Response output.
        /// </summary>
        public event Action<string> CaptureString;



        /// <summary>
        /// Event that allows you transform the stream as each chunk of
        /// the output is written in the Write() operation of the stream.
        /// This means that that it's possible/likely that the input
        /// buffer will not contain the full response output but only
        /// one of potentially many chunks.
        ///
        /// This event is called as part of the filter stream's Write()
        /// operation.
        /// </summary>
        public event Func<byte[], byte[]> TransformWrite;


        /// <summary>
        /// Event that allows you to transform the response stream as
        /// each chunk of bytep[] output is written during the stream's write
        /// operation. This means it's possibly/likely that the string
        /// passed to the handler only contains a portion of the full
        /// output. Typical buffer chunks are around 16k a piece.
        ///
        /// This event is called as part of the stream's Write operation.
        /// </summary>
        public event Func<string, string> TransformWriteString;

        /// <summary>
        /// This event allows capturing and transformation of the entire
        /// output stream by caching all write operations and delaying final
        /// response output until Flush() is called on the stream.
        /// </summary>
        public event Func<MemoryStream, MemoryStream> TransformStream;

        /// <summary>
        /// Event that can be hooked up to handle Response.Filter
        /// Transformation. Passed a string that you can modify and
        /// return back as a return value. The modified content
        /// will become the final output.
        /// </summary>
        public event Func<string, string> TransformString;


        protected virtual void OnCaptureStream(MemoryStream ms)
        {
            if (CaptureStream != null)
                CaptureStream(ms);
        }


        private void OnCaptureStringInternal(MemoryStream ms)
        {
            if (CaptureString != null)
            {
                string content = HttpContext.Current.Response.ContentEncoding.GetString(ms.ToArray());
                OnCaptureString(content);
            }
        }

        protected virtual void OnCaptureString(string output)
        {
            if (CaptureString != null)
                CaptureString(output);
        }

        protected virtual byte[] OnTransformWrite(byte[] buffer)
        {
            if (TransformWrite != null)
                return TransformWrite(buffer);
            return buffer;
        }

        private byte[] OnTransformWriteStringInternal(byte[] buffer)
        {
            Encoding encoding = HttpContext.Current.Response.ContentEncoding;
            string output = OnTransformWriteString(encoding.GetString(buffer));
            return encoding.GetBytes(output);
        }

        private string OnTransformWriteString(string value)
        {
            if (TransformWriteString != null)
                return TransformWriteString(value);
            return value;
        }


        protected virtual MemoryStream OnTransformCompleteStream(MemoryStream ms)
        {
            if (TransformStream != null)
                return TransformStream(ms);

            return ms;
        }




        /// <summary>
        /// Allows transforming of strings
        ///
        /// Note this handler is internal and not meant to be overridden
        /// as the TransformString Event has to be hooked up in order
        /// for this handler to even fire to avoid the overhead of string
        /// conversion on every pass through.
        /// </summary>
        /// <param name="responseText"></param>
        /// <returns></returns>
        private string OnTransformCompleteString(string responseText)
        {
            if (TransformString != null)
                TransformString(responseText);

            return responseText;
        }

        /// <summary>
        /// Wrapper method form OnTransformString that handles
        /// stream to string and vice versa conversions
        /// </summary>
        /// <param name="ms"></param>
        /// <returns></returns>
        internal MemoryStream OnTransformCompleteStringInternal(MemoryStream ms)
        {
            if (TransformString == null)
                return ms;

            //string content = ms.GetAsString();
            string content = HttpContext.Current.Response.ContentEncoding.GetString(ms.ToArray());

            content = TransformString(content);
            byte[] buffer = HttpContext.Current.Response.ContentEncoding.GetBytes(content);
            ms = new MemoryStream();
            ms.Write(buffer, 0, buffer.Length);
            //ms.WriteString(content);

            return ms;
        }

        /// <summary>
        ///
        /// </summary>
        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }
        /// <summary>
        ///
        /// </summary>
        public override bool CanWrite
        {
            get { return true; }
        }

        /// <summary>
        ///
        /// </summary>
        public override long Length
        {
            get { return 0; }
        }

        /// <summary>
        ///
        /// </summary>
        public override long Position
        {
            get { return _position; }
            set { _position = value; }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public override long Seek(long offset, System.IO.SeekOrigin direction)
        {
            return _stream.Seek(offset, direction);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="length"></param>
        public override void SetLength(long length)
        {
            _stream.SetLength(length);
        }

        /// <summary>
        ///
        /// </summary>
        public override void Close()
        {
            _stream.Close();
        }

        /// <summary>
        /// Override flush by writing out the cached stream data
        /// </summary>
        public override void Flush()
        {

            if (IsCaptured && _cacheStream.Length > 0)
            {
                // Check for transform implementations
                _cacheStream = OnTransformCompleteStream(_cacheStream);
                _cacheStream = OnTransformCompleteStringInternal(_cacheStream);

                OnCaptureStream(_cacheStream);
                OnCaptureStringInternal(_cacheStream);

                // write the stream back out if output was delayed
                if (IsOutputDelayed)
                    _stream.Write(_cacheStream.ToArray(), 0, (int)_cacheStream.Length);

                // Clear the cache once we've written it out
                _cacheStream.SetLength(0);
            }

            // default flush behavior
            _stream.Flush();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            return _stream.Read(buffer, offset, count);
        }


        /// <summary>
        /// Overriden to capture output written by ASP.NET and captured
        /// into a cached stream that is written out later when Flush()
        /// is called.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (IsCaptured)
            {
                // copy to holding buffer only - we'll write out later
                _cacheStream.Write(buffer, 0, count);
                _cachePointer += count;
            }

            // just transform this buffer
            if (TransformWrite != null)
                buffer = OnTransformWrite(buffer);
            if (TransformWriteString != null)
                buffer = OnTransformWriteStringInternal(buffer);

            if (!IsOutputDelayed)
                _stream.Write(buffer, offset, buffer.Length);

        }

    }

    public class MinifyEngine
    {
        private static char[] _lineSeparators = new char[] { '\n', '\r' };
        private static char[] _whiteSpaceSeparators = new char[] { ' ', '\t', '\n', '\r' };
        private static string[] _commentsMarkers = new string[] { "{", "}", "function", "var", "[if", "[endif" };
        private static string[] _blockElementsOpenStarts;
        private static string[] _blockElementsCloseStarts;
        static MinifyEngine()
        {
            var blockElements = new string[] {
            "article", "aside", "div", "dt", "caption", "footer", "form", "header", "hgroup", "html", "map", "nav", "section",
            "body", "p", "dl", "multicol", "dd", "blockquote", "figure", "address", "center",
            "title", "meta", "link", "html", "head", "body", "script", "br", "!DOCTYPE",
            "h1","h2","h3","h4","h5","h6", "pre", "ul", "menu", "dir", "ol", "li", "tr", "tbody", "thead", "tfoot", "td", "th" };

            _blockElementsOpenStarts = new string[blockElements.Length];
            _blockElementsCloseStarts = new string[blockElements.Length];
            for (int i = 0; i < blockElements.Length; i++)
            {
                _blockElementsOpenStarts[i] = "<" + blockElements[i];
                _blockElementsCloseStarts[i] = "</" + blockElements[i];
            }
        }

        private static Func<string, string> _minifyJS;
        private static Func<string, string> _minifyCSS;
        private bool _comments = true;
        private bool _aggressive = true;
        private bool _javascript = true;
        private bool _css = true;

        public Func<string, string> MinifyJS { set { _minifyJS = value; } }
        public Func<string, string> MinifyCSS { set { _minifyCSS = value; } }
        public bool Comments { set { _comments = value; } }
        public bool Aggressive { set { _aggressive = value; } }
        public bool Javascript { set { _javascript = value; } }
        public bool CSS { set { _css = value; } }

        public void AnalyseContent(string content, ref bool previousIsWhiteSpace, ref bool previousTokenEndsWithBlockElement)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            previousIsWhiteSpace = char.IsWhiteSpace(content[content.Length - 1]);
            previousTokenEndsWithBlockElement = EndsWithBlockElement(content);
        }

        public string Minify(string content, bool previousIsWhiteSpace, bool previousTokenEndsWithBlockElement)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(content.Length);

            if (_comments)
            {
                content = MinifyComments(content, builder);
            }

            content = MinifyJavascript(content, builder);
            content = MinifyInlineCSS(content, builder);

            content = MinifyAggressivelyHTML(content, builder, previousTokenEndsWithBlockElement);

            return content;
        }

        /// <summary>
        /// Removes all the comments that are not Javascript or IE conditional comments.
        /// </summary>
        /// <param name="content"></param>
        /// <param name="builder"></param>
        /// <returns></returns>
        private static string MinifyComments(string content, StringBuilder builder)
        {
            builder.Clear();
            var icommentstart = content.IndexOf("<!--");
            while (icommentstart >= 0)
            {
                var icommentend = content.IndexOf("-->", icommentstart + 3);
                if (icommentend < 0)
                {
                    break;
                }

                if (_commentsMarkers.Select(m => content.IndexOf(m, icommentstart)).Any(i => i > 0 && i < icommentend))
                {
                    // There is a comment but it contains javascript or IE conditionals
                    // => we keep it
                    break;
                }

                builder.Append(content, 0, icommentstart);
                builder.Append(content, icommentend + 3, content.Length - icommentend - 3);
                content = builder.ToString();
                builder.Clear();

                icommentstart = content.IndexOf("<!--", icommentstart);
            }
            return content;
        }

        /// <summary>
        /// Minify all the white space. Only one space is kept between attributes and words.
        /// Whitespace is completly remove arround HTML block elements while only a single
        /// one is kept arround inline elements.
        /// </summary>
        /// <param name="content"></param>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static string MinifyAggressivelyHTML(string content, StringBuilder builder, bool previousTokenEndsWithBlockElement)
        {
            builder.Clear();
            var tokens = content.Split(_whiteSpaceSeparators, StringSplitOptions.RemoveEmptyEntries);
            previousTokenEndsWithBlockElement |= (content.Length > 0) && !char.IsWhiteSpace(content[0]);
            for (int i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i];
                if (!previousTokenEndsWithBlockElement && !StartsWithBlockElement(token))
                {
                    // We have to keep a white space between 2 texts or an inline element and a text or between 2 inline elements
                    builder.Append(' ');
                }
                builder.Append(token);
                previousTokenEndsWithBlockElement = EndsWithBlockElement(tokens, i);
            }
            if (!previousTokenEndsWithBlockElement && char.IsWhiteSpace(content[content.Length - 1]))
            {
                builder.Append(' ');
            }
            content = builder.ToString();
            return content;
        }
        private static bool StartsWithBlockElement(string content)
        {
            return content[0] == '<' && (_blockElementsOpenStarts.Any(b => content.StartsWith(b)) || _blockElementsCloseStarts.Any(b => content.StartsWith(b)));
        }
        private static bool EndsWithBlockElement(string content)
        {
            if (content[content.Length - 1] != '>')
            {
                return false;
            }
            var istart = content.LastIndexOf('<');
            if (istart < 0)
            {
                return false;
            }
            return StartsWithBlockElement(content.Substring(istart));
        }
        private static bool EndsWithBlockElement(string[] tokens, int i)
        {
            var content = tokens[i];
            if (content[content.Length - 1] != '>')
            {
                return false;
            }
            int istart;
            for (istart = -1; istart < 0 && i >= 0; i--)
            {
                content = tokens[i];
                istart = content.LastIndexOf('<');
            }
            if (istart < 0)
            {
                return false;
            }
            return StartsWithBlockElement(content.Substring(istart));
        }

        /// <summary>
        /// Uses an external Javascript minifier to minimize inline JS code.
        /// </summary>
        /// <param name="content"></param>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static string MinifyJavascript(string content, StringBuilder builder)
        {
            builder.Clear();
            var iscriptstart = content.IndexOf("<script");
            while (iscriptstart >= 0)
            {
                var iscriptautoend = content.IndexOf("/>", iscriptstart + 7);
                var iscriptend = content.IndexOf("</script>", iscriptstart + 7);
                if ((iscriptend < 0) || ((iscriptautoend > 0) && (iscriptautoend < iscriptend)))
                {
                    break;
                }

                // We have some javascript code inside the tag
                // => we can ask a JS minifier to simplify it
                var istartcode = content.IndexOf('>', iscriptstart) + 1;
                var iendcode = iscriptend;
                string scritpTag = content.Substring(iscriptstart, istartcode - iscriptstart);
                var code = content.Substring(istartcode, iendcode - istartcode);
                builder.Append(content, 0, istartcode);

                if (!string.IsNullOrWhiteSpace(code))
                {
                    // We call the Microsoft JS minifier by reflexion to cut the dependency.
                    var minifiedCode = code;
                    try
                    {
                        if (!scritpTag.Contains("text/x-kendo-template") &&
                            !scritpTag.Contains("text/template"))
                        {
                            //minifiedCode = _minifyJS(code);
                            Minifier min = new Minifier();
                            minifiedCode = min.MinifyJavaScript(code, new CodeSettings()
                            {
                                MinifyCode = false,
                                TermSemicolons = false,

                            });
                        }
                    }
                    catch
                    {
                    }
                    builder.Append(minifiedCode);
                }

                iscriptstart = builder.Length;

                builder.Append(content, iscriptend, content.Length - iscriptend);
                content = builder.ToString();
                builder.Clear();

                iscriptstart = content.IndexOf("<script", iscriptstart);
            }
            return content;
        }

        /// <summary>
        /// Uses an external CSS minifier to minimize inline CSS code.
        /// </summary>
        /// <param name="content"></param>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static string MinifyInlineCSS(string content, StringBuilder builder)
        {
            builder.Clear();
            var iscriptstart = content.IndexOf("<style");
            while (iscriptstart >= 0)
            {
                var iscriptautoend = content.IndexOf("/>", iscriptstart + 6);
                var iscriptend = content.IndexOf("</style>", iscriptstart + 6);
                if ((iscriptend < 0) || ((iscriptautoend > 0) && (iscriptautoend < iscriptend)))
                {
                    break;
                }

                // We have some CSS code inside the tag
                // => we can ask a CSS minifier to simplify it
                var istartcode = content.IndexOf('>', iscriptstart) + 1;
                var iendcode = iscriptend;
                var code = content.Substring(istartcode, iendcode - istartcode);
                builder.Append(content, 0, istartcode);

                if (!string.IsNullOrWhiteSpace(code))
                {
                    // We call the Microsoft JS minifier by reflexion to cut the dependency.
                    var minifiedCode = code;
                    try
                    {
                        //minifiedCode = _minifyCSS(code);
                        Minifier min = new Minifier();
                        minifiedCode = min.MinifyStyleSheet(code, new CssSettings()
                        {
                            MinifyExpressions = false,
                            CommentMode = CssComment.None,

                        });
                    }
                    catch
                    {
                    }
                    builder.Append(minifiedCode);
                }

                iscriptstart = builder.Length;

                builder.Append(content, iscriptend, content.Length - iscriptend);
                content = builder.ToString();
                builder.Clear();

                iscriptstart = content.IndexOf("<style", iscriptstart);
            }
            return content;
        }
    }
}