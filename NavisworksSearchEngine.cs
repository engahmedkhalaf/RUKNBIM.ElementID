using Autodesk.Navisworks.Api;
using System.Collections.Generic;
using System.Linq;

namespace RUKNBIM.SmartSelect
{
    public class NavisworksSearchEngine
    {
        private List<string> _lastMissingIds = new List<string>();

        public void BuildCache(Document doc)
        {
            // Now a no-op, since we are moving to instantaneous native searching
        }

        public ModelItemCollection FindElements(IEnumerable<string> ids, Document doc)
        {
            var collection = new ModelItemCollection();
            _lastMissingIds = new List<string>(ids);

            var idList = ids.ToList();
            if (!idList.Any()) return collection;

            // Chunk the search into batches to keep the OR group manageable
            int batchSize = 500;
            for (int i = 0; i < idList.Count; i += batchSize)
            {
                var batch = idList.Skip(i).Take(batchSize).ToList();

                Search search = new Search();
                search.Selection.SelectAll();
                search.Locations = SearchLocations.DescendantsAndSelf;
                // DO NOT PruneBelowMatch here because we want to actually capture the item holding the Element ID

                List<SearchCondition> orGroup = new List<SearchCondition>();
                foreach (var id in batch)
                {
                    // To ensure we catch it, we search for both String and Int32 representations
                    if (int.TryParse(id, out int idInt))
                    {
                        orGroup.Add(SearchCondition.HasPropertyByDisplayName("Item", "Element Id")
                            .EqualValue(VariantData.FromInt32(idInt)));

                        orGroup.Add(SearchCondition.HasPropertyByDisplayName("Element ID", "Value")
                            .EqualValue(VariantData.FromInt32(idInt)));
                    }

                    orGroup.Add(SearchCondition.HasPropertyByDisplayName("Item", "Element Id")
                        .EqualValue(VariantData.FromDisplayString(id)));

                    orGroup.Add(SearchCondition.HasPropertyByDisplayName("Element ID", "Value")
                        .EqualValue(VariantData.FromDisplayString(id)));
                }

                // Add the group so they are evaluated as OR
                search.SearchConditions.AddGroup(orGroup);

                // Execute the native search
                var results = search.FindAll(doc, false);
                collection.AddRange(results);
            }

            // Figure out which ones were found to track missing ones
            foreach (var item in collection)
            {
                string id = GetRevitId(item);
                if (!string.IsNullOrEmpty(id))
                {
                    _lastMissingIds.Remove(id);
                }
            }

            return collection;
        }

        public List<string> GetMissingIds(IEnumerable<string> ids)
        {
            return _lastMissingIds;
        }

        private string GetRevitId(ModelItem item)
        {
            var prop = item.PropertyCategories.FindPropertyByDisplayName("Item", "Element Id");
            if (prop != null && prop.Value != null)
                return prop.Value.ToDisplayString();

            prop = item.PropertyCategories.FindPropertyByDisplayName("Element ID", "Value");
            if (prop != null && prop.Value != null)
                return prop.Value.ToDisplayString();

            return null;
        }
    }
}
