using System.Collections.Generic;

namespace RUKNBIM.ElementID
{
    public interface IDataService
    {
        List<string> ImportIdsFromExcel(string filePath);
        void ExportReportToExcel(string filePath, IEnumerable<string> foundIds, IEnumerable<string> missingIds);
    }
}
