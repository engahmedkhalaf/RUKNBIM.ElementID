using Autodesk.Navisworks.Api;
using System.Collections.Generic;
using System.Linq;

namespace RUKNBIM.ElementID
{
    public class NavisworksSearchEngine
    {
        // The property the original SelectByRevitId plugin used for Revit Element IDs.
        private const string DefaultCategory = "LcRevitId";
        private const string DefaultProperty = "LcOaNat64AttributeValue";

        // Keep the OR group manageable so the native search stays fast.
        private const int BatchSize = 1000;

        private List<string> _lastMissingIds = new List<string>();

        // Once we discover which category/property actually holds the Revit IDs,
        // remember it so we never have to scan the model again this session.
        private string _cachedCategory;
        private string _cachedProperty;

        public void BuildCache(Document doc)
        {
            // No-op: we rely on Navisworks' native indexed search.
        }

        public ModelItemCollection FindElements(IEnumerable<string> ids, Document doc)
        {
            var collection = new ModelItemCollection();
            var idList = ids.Select(s => s?.Trim())
                            .Where(s => !string.IsNullOrEmpty(s))
                            .Distinct()
                            .ToList();

            _lastMissingIds = new List<string>(idList);
            if (idList.Count == 0) return collection;

            var seen = new HashSet<ModelItem>();
            var remaining = new HashSet<string>(idList);

            // 1) Primary fast path: the known property, or a previously discovered one.
            string cat = _cachedCategory ?? DefaultCategory;
            string prop = _cachedProperty ?? DefaultProperty;
            SearchByProperty(doc, remaining.ToList(), cat, prop, collection, seen);
            UpdateRemaining(collection, remaining);

            // 2) Fallback: discover the real ID property once, cache it, then retry.
            if (remaining.Count > 0 && _cachedCategory == null)
            {
                if (DiscoverIdProperty(doc, out string discCat, out string discProp))
                {
                    _cachedCategory = discCat;
                    _cachedProperty = discProp;
                    SearchByProperty(doc, remaining.ToList(), discCat, discProp, collection, seen);
                    UpdateRemaining(collection, remaining);
                }
            }

            _lastMissingIds = remaining.ToList();
            return collection;
        }

        public List<string> GetMissingIds(IEnumerable<string> ids)
        {
            return _lastMissingIds;
        }

        // Runs one (batched) native search matching any of the given ids against a single property.
        private void SearchByProperty(Document doc, List<string> ids, string category, string property,
                                      ModelItemCollection collection, HashSet<ModelItem> seen)
        {
            for (int i = 0; i < ids.Count; i += BatchSize)
            {
                var batch = ids.Skip(i).Take(BatchSize).ToList();

                var search = new Search();
                search.Selection.SelectAll();
                search.Locations = SearchLocations.DescendantsAndSelf;

                var orGroup = new List<SearchCondition>();
                foreach (var id in batch)
                {
                    orGroup.Add(SearchCondition.HasPropertyByName(category, property)
                        .EqualValue(VariantData.FromDisplayString(id)));

                    // Also match when the value is stored as a numeric type.
                    if (int.TryParse(id, out int idInt))
                    {
                        orGroup.Add(SearchCondition.HasPropertyByName(category, property)
                            .EqualValue(VariantData.FromInt32(idInt)));
                    }
                }

                if (orGroup.Count == 0) continue;
                search.SearchConditions.AddGroup(orGroup);

                foreach (var item in search.FindAll(doc, false))
                {
                    if (seen.Add(item))
                        collection.Add(item);
                }
            }
        }

        // Removes ids we have now found from the remaining set.
        private void UpdateRemaining(ModelItemCollection collection, HashSet<string> remaining)
        {
            foreach (var item in collection)
            {
                string id = GetRevitId(item);
                if (!string.IsNullOrEmpty(id))
                    remaining.Remove(id);
            }
        }

        // Scans the model once to find a category/property that looks like a Revit Element ID.
        private bool DiscoverIdProperty(Document doc, out string category, out string property)
        {
            category = null;
            property = null;

            foreach (Model model in doc.Models)
            {
                if (model.RootItem == null) continue;

                foreach (var item in model.RootItem.DescendantsAndSelf)
                {
                    foreach (var cat in item.PropertyCategories)
                    {
                        foreach (var prop in cat.Properties)
                        {
                            if (prop.Value == null) continue;

                            bool looksLikeId = prop.DisplayName.ToLower().Contains("id")
                                            || prop.Name.ToLower().Contains("id");
                            if (!looksLikeId) continue;

                            string valStr = prop.Value.ToDisplayString();
                            if (int.TryParse(valStr, out int valInt) && valInt > 100)
                            {
                                category = cat.Name;
                                property = prop.Name;
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private string GetRevitId(ModelItem item)
        {
            // Prefer the property we are actually searching on.
            string cat = _cachedCategory ?? DefaultCategory;
            string prop = _cachedProperty ?? DefaultProperty;

            var p = item.PropertyCategories.FindPropertyByName(cat, prop);
            if (p != null && p.Value != null)
                return p.Value.ToDisplayString();

            // Fallback: any "id"-like property with an integer value.
            foreach (var category in item.PropertyCategories)
            {
                foreach (var property in category.Properties)
                {
                    if (property.Value == null) continue;

                    bool looksLikeId = property.DisplayName.ToLower().Contains("id")
                                    || property.Name.ToLower().Contains("id");
                    if (!looksLikeId) continue;

                    string val = property.Value.ToDisplayString();
                    if (int.TryParse(val, out _))
                        return val;
                }
            }

            return null;
        }
    }
}
