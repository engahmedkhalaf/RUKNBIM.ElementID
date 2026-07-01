using System.Collections.Generic;

namespace RUKNBIM.SmartSelect
{
    public interface IDataService
    {
        List<string> ImportIdsFromExcel(string filePath);
        void ExportReportToExcel(string filePath, IEnumerable<string> foundIds, IEnumerable<string> missingIds);
    }
}
