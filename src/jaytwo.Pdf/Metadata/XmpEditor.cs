using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace jaytwo.Pdf.Metadata;

internal static class XmpEditor
{
    internal const int MaxDocumentBytes = 32 * 1024 * 1024;

    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Pdf = "http://ns.adobe.com/pdf/1.3/";
    private static readonly XNamespace Xmp = "http://ns.adobe.com/xap/1.0/";
    private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private static readonly XNamespace X = "adobe:ns:meta/";

    private enum Shape
    {
        Simple,
        Alt,
        Seq,
    }

    public static bool TryParse(byte[] utf8, out XDocument? document, out Exception? error)
    {
        try
        {
            if (utf8.Length > MaxDocumentBytes)
            {
                throw new XmlException("XMP exceeds the 32 MiB document limit.");
            }

            using var stream = new MemoryStream(utf8, false);
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                MaxCharactersFromEntities = 0,
                MaxCharactersInDocument = MaxDocumentBytes,
                XmlResolver = null,
            };
            using var reader = XmlReader.Create(stream, settings);
            document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            document = null;
            error = ex;
            return false;
        }
    }

    public static byte[] ToBytes(XDocument document)
    {
        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = document.Declaration == null,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
        };
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, settings))
        {
            document.Save(writer);
        }

        return stream.ToArray();
    }

    public static XDocument CreateMinimal()
    {
        var description = new XElement(
            Rdf + "Description",
            new XAttribute(Rdf + "about", string.Empty),
            new XAttribute(XNamespace.Xmlns + "dc", Dc.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "pdf", Pdf.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xmp", Xmp.NamespaceName));
        return new XDocument(
            new XProcessingInstruction("xpacket", "begin=\"\uFEFF\" id=\"W5M0MpCehiHzreSzNTczkc9d\""),
            new XElement(
                X + "xmpmeta",
                new XAttribute(XNamespace.Xmlns + "x", X.NamespaceName),
                new XElement(
                    Rdf + "RDF",
                    new XAttribute(XNamespace.Xmlns + "rdf", Rdf.NamespaceName),
                    description)),
            new XProcessingInstruction("xpacket", "end=\"w\""));
    }

    /// <summary>
    /// Refuses a packet an ordinary edit cannot reach: one whose root is neither rdf:RDF nor an x:xmpmeta wrapper around it.
    /// </summary>
    public static void RequireEditable(XDocument document)
    {
        XElement? root = document.Root;
        bool editable = root != null
            && (root.Name == Rdf + "RDF" || (root.Name == X + "xmpmeta" && root.Element(Rdf + "RDF") != null));
        if (!editable)
        {
            throw new PdfXmpException("The XMP is not an rdf:RDF packet, so it cannot be edited.");
        }
    }

    public static bool IsMapped(string key)
    {
        return TryMap(key, out _, out _);
    }

    /// <summary>
    /// Refuses a touched property that is defined more than once, in competing forms, or with duplicate languages.
    /// Properties the edit does not touch, and packets without descriptions, are not examined.
    /// </summary>
    public static void CheckTouched(XDocument document, string key)
    {
        if (TryMap(key, out XName name, out Shape shape))
        {
            Locate(document, name, shape);
        }
    }

    public static void ApplyInfo(XDocument document, string key, string? value)
    {
        if (!TryMap(key, out XName name, out Shape shape))
        {
            return;
        }

        XElement? holder = Locate(document, name, shape);
        switch (shape)
        {
            case Shape.Alt:
                SetAlt(document, holder, name, value);
                break;
            case Shape.Seq:
                SetSeq(document, holder, name, value);
                break;
            default:
                SetSimple(document, holder, name, value);
                break;
        }
    }

    private static bool TryMap(string key, out XName name, out Shape shape)
    {
        shape = Shape.Simple;
        switch (key)
        {
            case "Title":
                name = Dc + "title";
                shape = Shape.Alt;
                return true;
            case "Subject":
                name = Dc + "description";
                shape = Shape.Alt;
                return true;
            case "Author":
                name = Dc + "creator";
                shape = Shape.Seq;
                return true;
            case "Keywords":
                name = Pdf + "Keywords";
                return true;
            case "Producer":
                name = Pdf + "Producer";
                return true;
            case "Creator":
                name = Xmp + "CreatorTool";
                return true;
            case "CreationDate":
                name = Xmp + "CreateDate";
                return true;
            case "ModDate":
                name = Xmp + "ModifyDate";
                return true;
            default:
                name = XName.Get("none");
                return false;
        }
    }

    // A touched property must be defined in exactly one place across the top-level descriptions, or not at all.
    // Returns the description that defines it, or null when no description does.
    private static XElement? Locate(XDocument document, XName name, Shape shape)
    {
        XElement? holder = null;
        int definitions = 0;
        foreach (XElement description in document.Descendants(Rdf + "RDF").SelectMany(rdf => rdf.Elements(Rdf + "Description")))
        {
            int here = description.Attributes(name).Count() + description.Elements(name).Count();
            if (here > 0)
            {
                holder = description;
                definitions += here;
            }
        }

        if (definitions > 1)
        {
            throw new PdfXmpException("The XMP defines " + name.LocalName + " more than once, so it cannot be edited.");
        }

        if (shape == Shape.Alt && holder != null)
        {
            XElement? element = holder.Element(name);
            var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (XElement li in element?.Descendants(Rdf + "li") ?? Enumerable.Empty<XElement>())
            {
                string? language = (string?)li.Attribute(XNamespace.Xml + "lang");
                if (language != null && !languages.Add(language))
                {
                    throw new PdfXmpException("The XMP " + name.LocalName + " repeats the language " + language + ", so it cannot be edited.");
                }
            }
        }

        return holder;
    }

    private static void SetSimple(XDocument document, XElement? holder, XName name, string? value)
    {
        XElement description = EnsureDescription(document, holder);
        XAttribute? attribute = description.Attribute(name);
        XElement? element = description.Element(name);
        if (value == null)
        {
            attribute?.Remove();
            element?.Remove();
            return;
        }

        if (attribute != null)
        {
            attribute.Value = value;
            return;
        }

        if (element == null)
        {
            element = new XElement(name);
            description.Add(element);
        }

        element.Value = value;
    }

    private static void SetAlt(XDocument document, XElement? holder, XName name, string? value)
    {
        XElement description = EnsureDescription(document, holder);
        XAttribute? attribute = description.Attribute(name);
        if (attribute != null && value != null)
        {
            attribute.Value = value;
            return;
        }

        XElement? element = description.Element(name);
        if (value == null)
        {
            attribute?.Remove();
            element?.Remove();
            return;
        }

        if (element == null)
        {
            element = new XElement(
                name,
                new XElement(
                    Rdf + "Alt",
                    new XElement(
                        Rdf + "li",
                        new XAttribute(XNamespace.Xml + "lang", "x-default"),
                        value)));
            description.Add(element);
            return;
        }

        XElement? li = element.Descendants(Rdf + "li").FirstOrDefault();
        if (li == null)
        {
            element.Add(new XElement(
                Rdf + "Alt",
                new XElement(Rdf + "li", new XAttribute(XNamespace.Xml + "lang", "x-default"), value)));
            return;
        }

        li.Value = value;
    }

    private static void SetSeq(XDocument document, XElement? holder, XName name, string? value)
    {
        XElement description = EnsureDescription(document, holder);
        XElement? element = description.Element(name);
        if (value == null)
        {
            element?.Remove();
            return;
        }

        if (element == null)
        {
            element = new XElement(name, new XElement(Rdf + "Seq", new XElement(Rdf + "li", value)));
            description.Add(element);
            return;
        }

        XElement? li = element.Descendants(Rdf + "li").FirstOrDefault();
        if (li == null)
        {
            element.Add(new XElement(Rdf + "Seq", new XElement(Rdf + "li", value)));
            return;
        }

        li.Value = value;
    }

    private static XElement EnsureDescription(XDocument document, XElement? holder)
    {
        XElement? description = holder ?? document.Descendants(Rdf + "Description").FirstOrDefault();
        if (description != null)
        {
            description.SetAttributeValue(XNamespace.Xmlns + "dc", Dc.NamespaceName);
            description.SetAttributeValue(XNamespace.Xmlns + "pdf", Pdf.NamespaceName);
            description.SetAttributeValue(XNamespace.Xmlns + "xmp", Xmp.NamespaceName);
            return description;
        }

        XElement rdf = document.Descendants(Rdf + "RDF").FirstOrDefault() ?? throw new PdfXmpException("XMP has no rdf:RDF element.");
        description = new XElement(
            Rdf + "Description",
            new XAttribute(Rdf + "about", string.Empty),
            new XAttribute(XNamespace.Xmlns + "dc", Dc.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "pdf", Pdf.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xmp", Xmp.NamespaceName));
        rdf.Add(description);
        return description;
    }
}
