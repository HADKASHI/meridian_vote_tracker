using Meridian.Domain.Csv;

namespace Meridian.Domain.Tests.Csv;

public sealed class CsvReaderTests
{
    [Fact]
    public void Quoted_fields_keep_commas_and_doubled_quotes()
    {
        var fields = CsvReader.SplitFields("MA/2024/036,\"Follow-up to the \"\"Blue Ocean\"\" Declaration, Including Capacity Building\",Y");

        Assert.Equal(["MA/2024/036", "Follow-up to the \"Blue Ocean\" Declaration, Including Capacity Building", "Y"], fields);
    }

    [Fact]
    public void A_leading_bom_is_dropped_and_crlf_and_lf_both_end_a_record()
    {
        var records = CsvReader.Read(new StringReader("﻿a,b\r\n1,2\n3,\r\n")).ToList();

        Assert.Equal([1, 2, 3], records.Select(r => r.LineNo));
        Assert.Equal(["a", "b"], records[0].Fields);
        Assert.Equal(["3", ""], records[2].Fields);
        Assert.Equal("1,2", records[1].RawText);
    }
}
