using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Xml;

namespace WeighingDesk;

internal static class XlsxExporter
{
    private const string SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static void Save(DataTable table, string path)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(archive, "[Content_Types].xml", writer =>
        {
            writer.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
            const string contentTypesNs = "http://schemas.openxmlformats.org/package/2006/content-types";
            writer.WriteStartElement("Default", contentTypesNs); writer.WriteAttributeString("Extension", "rels"); writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-package.relationships+xml"); writer.WriteEndElement();
            writer.WriteStartElement("Default", contentTypesNs); writer.WriteAttributeString("Extension", "xml"); writer.WriteAttributeString("ContentType", "application/xml"); writer.WriteEndElement();
            writer.WriteStartElement("Override", contentTypesNs); writer.WriteAttributeString("PartName", "/xl/workbook.xml"); writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"); writer.WriteEndElement();
            writer.WriteStartElement("Override", contentTypesNs); writer.WriteAttributeString("PartName", "/xl/worksheets/sheet1.xml"); writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"); writer.WriteEndElement();
            writer.WriteEndElement();
        });
        WriteEntry(archive, "_rels/.rels", writer =>
        {
            writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
            writer.WriteStartElement("Relationship", "http://schemas.openxmlformats.org/package/2006/relationships"); writer.WriteAttributeString("Id", "rId1"); writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"); writer.WriteAttributeString("Target", "xl/workbook.xml"); writer.WriteEndElement();
            writer.WriteEndElement();
        });
        WriteEntry(archive, "xl/workbook.xml", writer =>
        {
            writer.WriteStartElement("workbook", SpreadsheetNs);
            writer.WriteStartElement("sheets", SpreadsheetNs);
            writer.WriteStartElement("sheet", SpreadsheetNs); writer.WriteAttributeString("name", "Weighing records"); writer.WriteAttributeString("sheetId", "1"); writer.WriteAttributeString("r", "id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships", "rId1"); writer.WriteEndElement();
            writer.WriteEndElement(); writer.WriteEndElement();
        });
        WriteEntry(archive, "xl/_rels/workbook.xml.rels", writer =>
        {
            writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
            writer.WriteStartElement("Relationship", "http://schemas.openxmlformats.org/package/2006/relationships"); writer.WriteAttributeString("Id", "rId1"); writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"); writer.WriteAttributeString("Target", "worksheets/sheet1.xml"); writer.WriteEndElement();
            writer.WriteEndElement();
        });
        WriteEntry(archive, "xl/worksheets/sheet1.xml", writer =>
        {
            writer.WriteStartElement("worksheet", SpreadsheetNs);
            writer.WriteStartElement("sheetData", SpreadsheetNs);
            WriteRow(writer, 1, table.Columns.Cast<DataColumn>().Select(column => column.ColumnName), null);
            for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                var row = table.Rows[rowIndex];
                WriteRow(writer, rowIndex + 2, table.Columns.Cast<DataColumn>().Select(column => Convert.ToString(row[column], CultureInfo.InvariantCulture) ?? ""), table.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToArray());
            }
            writer.WriteEndElement(); writer.WriteEndElement();
        });
    }

    private static void WriteRow(XmlWriter writer, int rowNumber, IEnumerable<string> values, IReadOnlyCollection<string>? columnNames)
    {
        writer.WriteStartElement("row", SpreadsheetNs);
        writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
        var columnNumber = 0;
        foreach (var value in values)
        {
            var reference = ColumnName(++columnNumber) + rowNumber.ToString(CultureInfo.InvariantCulture);
            writer.WriteStartElement("c", SpreadsheetNs);
            writer.WriteAttributeString("r", reference);
            var name = columnNames?.ElementAt(columnNumber - 1);
            var isNumeric = name == "التسلسل" || name?.EndsWith("(كغ)", StringComparison.Ordinal) == true;
            if (isNumeric && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                writer.WriteElementString("v", SpreadsheetNs, number.ToString(CultureInfo.InvariantCulture));
            else
            {
                writer.WriteAttributeString("t", "inlineStr");
                writer.WriteStartElement("is", SpreadsheetNs);
                writer.WriteStartElement("t", SpreadsheetNs);
                writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
                writer.WriteString(value);
                writer.WriteEndElement(); writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static string ColumnName(int column)
    {
        var name = "";
        while (column > 0)
        {
            column--;
            name = (char)('A' + column % 26) + name;
            column /= 26;
        }
        return name;
    }

    private static void WriteEntry(ZipArchive archive, string name, Action<XmlWriter> write)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new System.Text.UTF8Encoding(false), Indent = false, CloseOutput = false });
        write(writer);
    }
}
