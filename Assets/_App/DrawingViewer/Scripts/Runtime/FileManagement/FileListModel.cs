using System.Collections.Generic;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Data model for the available files list used by FileBrowserUI.
    /// </summary>
    public class FileListModel
    {
        /// <summary>
        /// All available documents.
        /// </summary>
        public List<DrawingDocument> Documents { get; private set; } = new List<DrawingDocument>();

        /// <summary>
        /// Filtered/sorted view of documents (for search, sort, etc.).
        /// </summary>
        public List<DrawingDocument> FilteredDocuments { get; private set; } = new List<DrawingDocument>();

        /// <summary>
        /// Current sort mode.
        /// </summary>
        public SortMode CurrentSort { get; private set; } = SortMode.Name;

        /// <summary>
        /// Sort modes for the file list.
        /// </summary>
        public enum SortMode
        {
            Name,
            DateAdded,
            PageCount
        }

        /// <summary>
        /// Updates the document list.
        /// </summary>
        public void SetDocuments(List<DrawingDocument> documents)
        {
            Documents = documents ?? new List<DrawingDocument>();
            ApplySort();
        }

        /// <summary>
        /// Filters documents by a search query.
        /// </summary>
        public void Filter(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                FilteredDocuments = new List<DrawingDocument>(Documents);
            }
            else
            {
                string lowerQuery = query.ToLowerInvariant();
                FilteredDocuments = Documents.FindAll(d =>
                    d.Name.ToLowerInvariant().Contains(lowerQuery));
            }
        }

        /// <summary>
        /// Sets the sort mode and re-sorts.
        /// </summary>
        public void SetSortMode(SortMode mode)
        {
            CurrentSort = mode;
            ApplySort();
        }

        /// <summary>
        /// Applies the current sort to the filtered list.
        /// </summary>
        private void ApplySort()
        {
            var sorted = new List<DrawingDocument>(Documents);

            switch (CurrentSort)
            {
                case SortMode.Name:
                    sorted.Sort((a, b) => a.Name.CompareTo(b.Name));
                    break;
                case SortMode.DateAdded:
                    sorted.Sort((a, b) => b.ImportDate.CompareTo(a.ImportDate));
                    break;
                case SortMode.PageCount:
                    sorted.Sort((a, b) => a.PageCount.CompareTo(b.PageCount));
                    break;
            }

            FilteredDocuments = sorted;
        }
    }
}
