using Microsoft.Diagnostics.NETCore.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Serialization;
using TinyVolumeAdjuster;

namespace InteractiveVision
{
    /// <summary>
    /// 未捕获异常处理
    /// </summary>
    public static class HandleBug
    {
        private static Action OnException = () => { };
        private static string _path;

        public static void Go(string path, Action? lastProcess = null)
        {
            _path = path;
            if (lastProcess != null)
                OnException = lastProcess;

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            Dispatcher.CurrentDispatcher?.UnhandledException += HandleBug_UnhandledException;
            Application.Current?.DispatcherUnhandledException += HandleBug_DispatcherUnhandledException;
        }

        private static void ExceptionHandler(Exception? ex)
        {
            if (ex != null)
            {
                try
                {
                    if (!Directory.Exists(_path))
                        Directory.CreateDirectory(_path);
                    var ts = $"{DateTime.Now:yyyyMMddHHmmss}";
                    string logPath = Path.Combine(_path, $"ErrorLog-{ts}.xml");
                    string dmpPath = Path.Combine(_path, $"Crash-{ts}.dmp");
                    File.AppendAllText(logPath, new SerializableException(ex).ToString(), Encoding.UTF8);
                    var client = new DiagnosticsClient(Environment.ProcessId);
                    client.WriteDump(DumpType.Normal, dmpPath);
                }
                catch { }
            }

            OnException();
        }

        private static void HandleBug_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ExceptionHandler(e.Exception);
            e.Handled = true;
            Environment.Exit(0);
        }

        private static void HandleBug_UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ExceptionHandler(e.Exception);
            e.Handled = true;
            Environment.Exit(0);
        }

        private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            ExceptionHandler(e.Exception);
            e.SetObserved();
            Environment.Exit(0);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ExceptionHandler(e.ExceptionObject as Exception);
            Environment.Exit(0);
        }
    }

    [Serializable]
    [XmlRoot("dictionary")]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IXmlSerializable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SerializableDictionary{TKey,TValue}"/> class.
        /// This is the default constructor provided for XML serializer.
        /// </summary>
        public SerializableDictionary()
        {
        }

        public SerializableDictionary(IDictionary<TKey, TValue> dictionary)
        {
            if (dictionary == null)
            {
                throw new ArgumentNullException();
            }

            foreach (var pair in dictionary)
            {
                this.Add(pair.Key, pair.Value);
            }
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            /*if (reader.IsEmptyElement)
            {
                return;
            }*/
            var inner = reader.ReadSubtree();

            var xElement = XElement.Load(inner);
            if (xElement.HasElements)
            {
                foreach (var element in xElement.Elements())
                {
                    this.Add((TKey)Convert.ChangeType(element.Name.ToString(), typeof(TKey)), (TValue)Convert.ChangeType(element.Value, typeof(TValue)));
                }
            }

            inner.Close();

            reader.ReadEndElement();
        }

        public void WriteXml(XmlWriter writer)
        {
            foreach (var key in this.Keys)
            {
                writer.WriteStartElement(key.ToString().Replace(" ", ""));
                // if it's Serializable doesn't mean serialization will succeed (IE. GUID and SQLError types)
                try
                {
                    writer.WriteValue(this[key]);
                }
                catch (Exception)
                {
                    // we're not Throwing anything here, otherwise evil thing will happen
                    writer.WriteValue(this[key].ToString());
                }
                writer.WriteEndElement();
            }
        }
    }

    [Serializable]
    public class SerializableException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SerializableException"/> class.
        /// Default constructor provided for XML serialization and de-serialization.
        /// </summary>
        public SerializableException()
        {
        }

        public SerializableException(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException();
            }

            var oldCulture = Thread.CurrentThread.CurrentCulture;
            var oldUICulture = Thread.CurrentThread.CurrentUICulture;
            try
            {
                // Prefer messages in English, instead of in language of the user.
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

                this.Type = exception.GetType().ToString();

                if (exception.Data.Count != 0)
                {
                    foreach (DictionaryEntry entry in exception.Data)
                    {
                        if (entry.Value != null)
                        {
                            // Assign 'Data' property only if there is at least one entry with non-null value
                            if (this.Data == null)
                            {
                                this.Data = new SerializableDictionary<object, object>();
                            }

                            this.Data.Add(entry.Key, entry.Value);
                        }
                    }
                }

                if (exception.HelpLink != null)
                {
                    this.HelpLink = exception.HelpLink;
                }

                if (exception.InnerException != null)
                {
                    this.InnerException = new SerializableException(exception.InnerException);
                }

                if (exception is AggregateException)
                {
                    this.InnerExceptions = new List<SerializableException>();

                    foreach (var innerException in ((AggregateException)exception).InnerExceptions)
                    {
                        this.InnerExceptions.Add(new SerializableException(innerException));
                    }

                    this.InnerExceptions.RemoveAt(0);
                }

                this.Message = exception.Message != string.Empty ? exception.Message : string.Empty;

                if (exception.Source != null)
                {
                    this.Source = exception.Source;
                }

                if (exception.StackTrace != null)
                {
                    this.StackTrace = exception.StackTrace;
                }

                if (exception.TargetSite != null)
                {
                    this.TargetSite = string.Format("{0} @ {1}", exception.TargetSite, exception.TargetSite.DeclaringType);
                }

                this.ExtendedInformation = this.GetExtendedInformation(exception);

            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = oldCulture;
                Thread.CurrentThread.CurrentUICulture = oldUICulture;
            }
        }

        public SerializableDictionary<object, object> Data { get; set; }

        public SerializableDictionary<string, object> ExtendedInformation { get; set; }

        public string HelpLink { get; set; }

        public SerializableException InnerException { get; set; }

        public List<SerializableException> InnerExceptions { get; set; }

        public string Message { get; set; }

        public string Source { get; set; }

        public string StackTrace { get; set; }

        // This will make TargetSite property XML serializable but RuntimeMethodInfo class does not have a parameterless
        // constructor thus the serializer throws an exception if full info is used
        public string TargetSite { get; set; }

        public string Type { get; set; }

        public override string ToString()
        {
            var serializer = new XmlSerializer(typeof(SerializableException));
            using (var stream = new MemoryStream())
            {
                stream.SetLength(0);
                serializer.Serialize(stream, this);
                stream.Position = 0;
                var doc = XDocument.Load(stream);
                return doc.Root.ToString();
            }
        }

        private SerializableDictionary<string, object> GetExtendedInformation(Exception exception)
        {
            var extendedProperties = (from property in exception.GetType().GetProperties()
                                      where
                                          property.Name != "Data" && property.Name != "InnerExceptions" && property.Name != "InnerException"
                                          && property.Name != "Message" && property.Name != "Source" && property.Name != "StackTrace"
                                          && property.Name != "TargetSite" && property.Name != "HelpLink" && property.CanRead
                                      select property).ToArray();

            if (extendedProperties.Any())
            {
                var extendedInformation = new SerializableDictionary<string, object>();

                foreach (var property in extendedProperties.Where(property => property.GetValue(exception, null) != null))
                {
                    extendedInformation.Add(property.Name, property.GetValue(exception, null));
                }

                return extendedInformation;
            }
            else
            {
                return null;
            }
        }
    }
}
