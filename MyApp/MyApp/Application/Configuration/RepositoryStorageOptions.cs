using System;

namespace MyApp.Application.Configuration
{
    public sealed class RepositoryStorageOptions
    {
        private string _rootPath = string.Empty;

        public string RootPath
        {
            get
            {
                return _rootPath;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _rootPath = value;
            }
        }
    }
}
