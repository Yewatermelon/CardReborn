using System;
using Card.Core;
using NUnit.Framework;

namespace Card.Tests.EditMode.Core
{
    /// <summary>M2-T2：配置源表用的极简 CSV 解析器。</summary>
    public sealed class CsvTableTests
    {
        [Test]
        public void Parse_WhenSimpleTable_ReadsHeaderAndRows()
        {
            CsvTable table = CsvTable.Parse("Id,Key\n1,ALPHA\n2,BETA\n");

            Assert.That(table.Header, Is.EqualTo(new[] { "Id", "Key" }));
            Assert.That(table.ColumnCount, Is.EqualTo(2));
            Assert.That(table.RowCount, Is.EqualTo(2));
            Assert.That(table.GetCell(0, "Key"), Is.EqualTo("ALPHA"));
            Assert.That(table.GetCell(1, "Id"), Is.EqualTo("2"));
        }

        [Test]
        public void Parse_WhenBomPresent_StripsItFromFirstHeader()
        {
            CsvTable table = CsvTable.Parse("\uFEFFId,Key\n1,ALPHA\n");

            Assert.That(table.Header[0], Is.EqualTo("Id"));
            Assert.That(table.HasColumn("Id"), Is.True);
        }

        [Test]
        public void Parse_WhenCrlf_HandlesLineEndings()
        {
            CsvTable table = CsvTable.Parse("Id,Key\r\n1,ALPHA\r\n");

            Assert.That(table.RowCount, Is.EqualTo(1));
            Assert.That(table.GetCell(0, "Key"), Is.EqualTo("ALPHA"));
        }

        [Test]
        public void Parse_WhenBlankLinesPresent_SkipsThem()
        {
            CsvTable table = CsvTable.Parse("Id,Key\n\n1,ALPHA\n   \n2,BETA\n");

            Assert.That(table.RowCount, Is.EqualTo(2));
            Assert.That(table.GetCell(1, "Key"), Is.EqualTo("BETA"));
        }

        [Test]
        public void Parse_WhenHeaderOnly_HasNoRows()
        {
            CsvTable table = CsvTable.Parse("Id,Key\n");

            Assert.That(table.RowCount, Is.EqualTo(0));
            Assert.That(table.ColumnCount, Is.EqualTo(2));
        }

        [Test]
        public void Parse_WhenTextIsNull_ThrowsArgumentNullException()
        {
            ArgumentNullException exception =
                Assert.Throws<ArgumentNullException>(() => CsvTable.Parse(null!))!;

            Assert.That(exception.ParamName, Is.EqualTo("text"));
        }

        [Test]
        public void Parse_WhenHeaderMissing_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => CsvTable.Parse("\n\n"));
        }

        [Test]
        public void Parse_WhenRowHasMoreCellsThanHeader_ThrowsWithLineNumber()
        {
            FormatException exception = Assert.Throws<FormatException>(
                () => CsvTable.Parse("Id,Key\n1,ALPHA,EXTRA\n"))!;

            Assert.That(exception.Message, Does.Contain("第 2 行"));
            Assert.That(exception.Message, Does.Contain("多于表头"));
        }

        [Test]
        public void Parse_WhenRowHasFewerCellsThanHeader_PadsWithEmpty()
        {
            CsvTable table = CsvTable.Parse("Id,Key,Effects\n1,ALPHA\n");

            Assert.That(table.GetCell(0, "Effects"), Is.Empty);
        }

        [Test]
        public void Parse_WhenCellsHaveWhitespace_TrimsThem()
        {
            CsvTable table = CsvTable.Parse(" Id , Key \n 1 , ALPHA \n");

            Assert.That(table.Header, Is.EqualTo(new[] { "Id", "Key" }));
            Assert.That(table.GetCell(0, "Key"), Is.EqualTo("ALPHA"));
        }

        [Test]
        public void IndexOfColumn_WhenColumnMissing_ReturnsMinusOne()
        {
            CsvTable table = CsvTable.Parse("Id,Key\n1,ALPHA\n");

            Assert.That(table.IndexOfColumn("Nope"), Is.EqualTo(-1));
            Assert.That(table.HasColumn("Nope"), Is.False);
            Assert.That(table.HasColumn("Key"), Is.True);
        }

        [Test]
        public void IndexOfColumn_WhenCaseDiffers_IsCaseSensitive()
        {
            CsvTable table = CsvTable.Parse("Id,Key\n1,ALPHA\n");

            Assert.That(table.HasColumn("key"), Is.False, "表头大小写写错必须暴露");
        }

        [Test]
        public void GetCell_WhenColumnMissing_ThrowsArgumentException()
        {
            CsvTable table = CsvTable.Parse("Id,Key\n1,ALPHA\n");

            ArgumentException exception =
                Assert.Throws<ArgumentException>(() => table.GetCell(0, "Nope"))!;

            Assert.That(exception.ParamName, Is.EqualTo("columnName"));
            Assert.That(exception.Message, Does.Contain("Nope"));
        }

        [Test]
        public void GetRow_WhenIndexOutOfRange_ThrowsArgumentOutOfRangeException()
        {
            CsvTable table = CsvTable.Parse("Id,Key\n1,ALPHA\n");

            Assert.Throws<ArgumentOutOfRangeException>(() => table.GetRow(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => table.GetRow(-1));
        }
    }
}
