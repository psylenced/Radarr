using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.MediaFiles.MovieImport.Specifications
{
    public class UpgradeSpecification : IImportDecisionEngineSpecification
    {
        private readonly ICustomFormatCalculationService _customFormatCalculationService;
        private readonly Logger _logger;

        public UpgradeSpecification(ICustomFormatCalculationService customFormatCalculationService, Logger logger)
        {
            _customFormatCalculationService = customFormatCalculationService;
            _logger = logger;
        }

        public Decision IsSatisfiedBy(LocalMovie localMovie, DownloadClientItem downloadClientItem)
        {
            var profiles = localMovie.Movie.QualityProfiles.Value;
            var files = localMovie.Movie.MovieFiles.Value;
            var isUpgradeForAny = false;

            foreach (var profile in profiles)
            {
                var qualityComparer = new QualityModelComparer(profile);
                var preferredWordScore = GetCustomFormatScore(profile, localMovie);

                foreach (var file in files)
                {
                    var movieFile = file;

                    if (movieFile == null)
                    {
                        _logger.Trace("Unable to get movie file details from the DB. MovieId: {0} MovieFileId: {1}", localMovie.Movie.Id, file.Id);
                        continue;
                    }

                    var qualityCompare = qualityComparer.Compare(localMovie.Quality.Quality, movieFile.Quality.Quality);

                    if (qualityCompare < 0)
                    {
                        _logger.Debug("This file isn't a quality upgrade for movie. Skipping {0}", localMovie.Path);
                        continue;
                    }

                    var customFormats = _customFormatCalculationService.ParseCustomFormat(file);
                    var movieFileCustomFormatScore = profile.CalculateCustomFormatScore(customFormats);

                    if (qualityCompare == 0 && preferredWordScore < movieFileCustomFormatScore)
                    {
                        _logger.Debug("This file isn't a custom format upgrade for movie. Skipping {0}", localMovie.Path);
                        continue;
                    }

                    isUpgradeForAny = true;
                }
            }

            if (files.Count > 0 && !isUpgradeForAny)
            {
                return Decision.Reject("File is not a quality or custom format upgrade for any profile for movie.");
            }

            return Decision.Accept();
        }

        private int GetCustomFormatScore(Profile profile, LocalMovie localMovie)
        {
            var movie = localMovie.Movie;
            var fileFormats = new List<CustomFormat>();
            var folderFormats = new List<CustomFormat>();
            var clientFormats = new List<CustomFormat>();

            if (localMovie.FileMovieInfo != null)
            {
                fileFormats = _customFormatCalculationService.ParseCustomFormat(localMovie.FileMovieInfo, movie);
            }

            if (localMovie.FolderMovieInfo != null)
            {
                folderFormats = _customFormatCalculationService.ParseCustomFormat(localMovie.FolderMovieInfo, movie);
            }

            if (localMovie.DownloadClientMovieInfo != null)
            {
                clientFormats = _customFormatCalculationService.ParseCustomFormat(localMovie.DownloadClientMovieInfo, movie);
            }

            var formats = fileFormats.Union(folderFormats.Union(clientFormats)).ToList();

            return profile.CalculateCustomFormatScore(formats);
        }
    }
}
