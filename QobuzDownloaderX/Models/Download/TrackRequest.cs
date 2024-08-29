using DownloadAssistant.Options;
using DownloadAssistant.Requests;
using Newtonsoft.Json.Linq;
using PlaylistsNET.Content;
using PlaylistsNET.Models;
using QobuzApiSharp.Exceptions;
using QobuzApiSharp.Models.Content;
using QobuzApiSharp.Service;
using QobuzDownloaderX.Models.Content;
using QobuzDownloaderX.Properties;
using QobuzDownloaderX.Shared;
using Requests;
using Requests.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ZendeskApi_v2.Requests;

namespace QobuzDownloaderX.Models.Download
{
    internal class TrackRequest : Request<TrackRequestOptions, DownloadItem, DownloadItem>
    {
        private readonly DownloadLogger _logger;
        private readonly DownloadItem _downloadItem;
        private readonly string _downloadPath;
        private DownloadItemInfo _downloadInfo;
        private DownloadItemPaths _downloadPaths;
        private bool _isAlbum = false;
        private readonly RequestContainer<IRequest> _requestContainer = [];

        public TrackRequest(TrackRequestOptions options)
            : base(options)
        {
            _logger = options.Logger;
            _downloadItem = options.DownloadItem;
            _downloadPath = options.DownloadPath;
            AutoStart();
        }

        protected override async Task<RequestReturn> RunRequestAsync()
        {
            RequestReturn requestReturn = new();
            try
            {
                requestReturn.Successful = await StartDownloadItemTaskAsync();
            }
            catch (Exception ex)
            {
                requestReturn.Successful = false;
                AddException(ex);
            }
            return requestReturn;
        }

        private async Task<bool> StartDownloadItemTaskAsync()
        {
            string logLine = $"Downloading <{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(_downloadItem.Type)}> from {_downloadItem.Url}";
            _logger.AddDownloadLogLine(new string('=', logLine.Length).PadRight(logLine.Length), true);
            _logger.AddDownloadLogLine(logLine, true);
            _logger.AddDownloadLogLine(new string('=', logLine.Length).PadRight(logLine.Length), true);
            _logger.AddEmptyDownloadLogLine(true);

            _downloadInfo = new DownloadItemInfo
            {
                DownloadItemID = _downloadItem.Id
            };
            _downloadPaths = _downloadInfo.CurrentDownloadPaths;

            switch (_downloadItem.Type)
            {
                case "track":
                    return await StartDownloadTrackTaskAsync();
                case "album":
                    return await StartDownloadAlbumTaskAsync();
                case "artist":
                    return await StartDownloadArtistDiscogTaskAsync();
                case "label":
                    return await StartDownloadLabelTaskAsync();

                case "user":
                    if (_downloadInfo.DownloadItemID == @"library/favorites/albums")
                        return await StartDownloadFaveAlbumsTaskAsync();
                    else if (_downloadInfo.DownloadItemID == @"library/favorites/artists")
                        return await StartDownloadFaveArtistsTaskAsync();
                    else if (_downloadInfo.DownloadItemID == @"library/favorites/tracks")
                        return await StartDownloadFaveTracksTaskAsync();
                    else
                    {
                        _logger.AddDownloadLogLine($"You entered an invalid user favorites link.{Environment.NewLine}", true, true);
                        _logger.AddDownloadLogLine($"Favorite Tracks, Albums & Artists are supported with the following links:{Environment.NewLine}", true, true);
                        _logger.AddDownloadLogLine($"Tracks - https://play.qobuz.com/user/library/favorites/tracks{Environment.NewLine}", true, true);
                        _logger.AddDownloadLogLine($"Albums - https://play.qobuz.com/user/library/favorites/albums{Environment.NewLine}", true, true);
                        _logger.AddDownloadLogLine($"Artists - https://play.qobuz.com/user/library/favorites/artists{Environment.NewLine}", true, true);
                        Options.NumberOfAttempts = 0;
                        return false;
                    }

                case "playlist":
                    return await StartDownloadPlaylistTaskAsync();
                default:
                    // We shouldn't get here?!? I'll leave this here just in case...
                    _logger.AddDownloadLogLine($"URL >{_downloadItem.Url}< not understood. Is there a typo?", true, true);
                    Options.NumberOfAttempts = 0;
                    return false;
            }
        }


        private T ExecuteApiCall<T>(Func<QobuzApiService, T> apiCall)
        {
            try
            {
                return apiCall(QobuzApiServiceManager.GetApiService());
            }
            catch (Exception ex)
            {
                AddException(ex);
                List<string> errorLines = [];

                _logger.AddEmptyDownloadLogLine(false, true);
                _logger.AddDownloadLogErrorLine($"Communication problem with Qobuz API. Details saved to error log{Environment.NewLine}", true, true);

                switch (ex)
                {
                    case ApiErrorResponseException erEx:
                        errorLines.Add("Failed API request:");
                        errorLines.Add(erEx.RequestContent);
                        errorLines.Add($"Api response code: {erEx.ResponseStatusCode}");
                        errorLines.Add($"Api response status: {erEx.ResponseStatus}");
                        errorLines.Add($"Api response reason: {erEx.ResponseReason}");
                        break;
                    case ApiResponseParseErrorException pEx:
                        errorLines.Add("Error parsing API response");
                        errorLines.Add($"Api response content: {pEx.ResponseContent}");
                        break;
                    default:
                        errorLines.Add("Unknown error trying API request:");
                        errorLines.Add($"{ex}");
                        break;
                }

                // Write detailed info to error log
                _logger.AddDownloadErrorLogLines(errorLines.ToArray());
            }

            return default;
        }

        public bool IsStreamable(Track qobuzTrack, bool inPlaylist = false)
        {
            if (qobuzTrack.Streamable != false) return true;

            bool tryToStream = true;

            switch (Options.CheckIfStreamable)
            {
                case true:
                    string trackReference;

                    if (inPlaylist)
                    {
                        trackReference = $"{qobuzTrack.Performer?.Name} - {qobuzTrack.Title}";
                    }
                    else
                    {
                        trackReference = $"{qobuzTrack.TrackNumber.GetValueOrDefault()} {StringTools.DecodeEncodedNonAsciiCharacters(qobuzTrack.Title.Trim())}";
                    }

                    _logger.AddDownloadLogLine($"Track {trackReference} is not available for streaming. Unable to download.\r\n", true, true);
                    tryToStream = false;
                    break;

                default:
                    _logger.AddDownloadLogLine("Track is not available for streaming. But streamable check is being ignored for debugging, or messed up releases. Attempting to download...\r\n", tryToStream, true);
                    break;
            }

            return tryToStream;
        }

        private bool DownloadTrack(Track qobuzTrack, string basePath, bool isPartOfTracklist, bool removeTagArtFileAfterDownload = false, string albumPathSuffix = "")
        {
            if (State != RequestState.Running || Token.IsCancellationRequested)
                return false;

            string trackIdString = qobuzTrack.Id.GetValueOrDefault().ToString();
            _downloadInfo.SetTrackTaggingInfo(qobuzTrack);

            if (!_isAlbum)
            {
                _downloadInfo.SetAlbumDownloadInfo(qobuzTrack.Album);
                Options.UpdateAlbumTagsUi.Invoke(_downloadInfo);
            }

            if (!IsStreamable(qobuzTrack, isPartOfTracklist))
                return ReturnFail();

            CreateTrackDirectories(basePath, _downloadPaths.QualityPath, albumPathSuffix, isPartOfTracklist);
            string trackPath = _downloadInfo.CurrentDownloadPaths.Path4Full;

            if (!PrepareTrackFile(trackPath, isPartOfTracklist))
                return true;

            string streamUrl = ExecuteApiCall(apiService => apiService.GetTrackFileUrl(trackIdString, Globals.FormatIdString))?.Url;

            if (string.IsNullOrEmpty(streamUrl))
            {
                _logger.AddDownloadLogLine($"Couldn't get streaming URL for Track \"{_downloadPaths.FinalTrackNamePath}\". Skipping.\r\n", true, true);
                return ReturnFail();
            }

            _requestContainer.Add(DownloadTrackFile(streamUrl, trackPath));
            _requestContainer.Add(DownloadCoverArt(isPartOfTracklist));
            TagMetadata();

            if (removeTagArtFileAfterDownload)
                RemoveTempTaggingArtFile();

            _logger.AddDownloadLogLine("Track Download Done!\r\n", true, true);

            return true;
        }

        private bool PrepareTrackFile(string trackPath, bool isPartOfTracklist)
        {
            string paddedTrackNumber = _downloadInfo.TrackNumber.ToString().PadLeft(Math.Max(2, (int)Math.Floor(Math.Log10(_downloadInfo.TrackTotal) + 1)), '0');

            if (isPartOfTracklist)
                _downloadPaths.FinalTrackNamePath = string.Concat(_downloadPaths.PerformerNamePath, Globals.FileNameTemplateString, _downloadPaths.TrackNamePath).TrimEnd();
            else
                _downloadPaths.FinalTrackNamePath = string.Concat(paddedTrackNumber, Globals.FileNameTemplateString, _downloadPaths.TrackNamePath).TrimEnd();

            _downloadPaths.FinalTrackNamePath = StringTools.TrimToMaxLength(_downloadPaths.FinalTrackNamePath, Globals.MaxLength);
            _downloadPaths.FullTrackFileName = _downloadPaths.FinalTrackNamePath + Globals.AudioFileType;
            _downloadPaths.FullTrackFilePath = Path.Combine(trackPath, _downloadPaths.FullTrackFileName);

            if (File.Exists(_downloadPaths.FullTrackFilePath))
            {
                string message = $"File for \"{_downloadPaths.FinalTrackNamePath}\" already exists. Skipping.\r\n";
                _logger.AddDownloadLogLine(message, true, true);
                return false;
            }

            _logger.AddDownloadLogLine($"Downloading - {_downloadPaths.FinalTrackNamePath} ...... ", true, true);
            return true;
        }

        private GetRequest DownloadTrackFile(string streamUrl, string trackPath)
        {
            var options = new GetRequestOptions()
            {
                IsDownload = true,
                DirectoryPath = trackPath,
                Filename = _downloadPaths.FullTrackFileName,
                NumberOfAttempts = 3,
                RequestCancelled = (req) => HandleDownloadException(req.Exception, "Track Download canceled, probably due to network error or request timeout."),
                RequestFailed = (req, _) => HandleDownloadException(req.Exception, "Unknown error during Track Download.")
            };
            return new GetRequest(streamUrl, options);
        }

        private GetRequest DownloadCoverArt(bool isPartOfTracklist)
        {
            string coverArtTagFilePath = Path.Combine(_downloadPaths.Path3Full, Globals.TaggingOptions.ArtSize + ".jpg");
            if (!File.Exists(coverArtTagFilePath))
            {
                var options = new GetRequestOptions()
                {
                    IsDownload = true,
                    DirectoryPath = _downloadPaths.Path3Full,
                    Filename = Globals.TaggingOptions.ArtSize + ".jpg",
                    NumberOfAttempts = 1,
                    RequestFailed = (req, _) => _logger.AddDownloadErrorLogLines(["Error downloading image file for tagging.", req.Exception.Message, Environment.NewLine])
                };
                return new GetRequest(_downloadInfo.FrontCoverImgTagUrl, options);
            }

            string coverArtFilePath = Path.Combine(_downloadPaths.Path3Full, "Cover.jpg");
            if (!isPartOfTracklist && !File.Exists(coverArtFilePath))
            {
                var options = new GetRequestOptions()
                {
                    IsDownload = true,
                    DirectoryPath = _downloadPaths.Path3Full,
                    Filename = "Cover.jpg",
                    NumberOfAttempts = 1,
                    RequestFailed = (req, _) => _logger.AddDownloadErrorLogLines(new string[] { "Error downloading full size cover image file.", req.Exception.Message, Environment.NewLine })
                };
                return new GetRequest(_downloadInfo.FrontCoverImgUrl, options);
            }
            return null;
        }

        private void TagMetadata()
        {
            string coverArtTagFilePath = Path.Combine(_downloadPaths.Path3Full, Globals.TaggingOptions.ArtSize + ".jpg");
            AudioFileTagger.AddMetaDataTags(_downloadInfo, _downloadPaths.FullTrackFilePath, coverArtTagFilePath, _logger.logPath, _logger);
        }

        private void RemoveTempTaggingArtFile()
        {
            string coverArtTagFilePath = Path.Combine(_downloadPaths.Path3Full, Globals.TaggingOptions.ArtSize + ".jpg");
            if (File.Exists(coverArtTagFilePath))
                File.Delete(coverArtTagFilePath);
        }

        private void HandleDownloadException(Exception ex, string message)
        {
            _logger.AddDownloadLogErrorLine($"{message} Details saved to error log.{Environment.NewLine}", true, true);
            _logger.AddDownloadErrorLogLine(message);
            _logger.AddDownloadErrorLogLine(ex.ToString());
            _logger.AddDownloadErrorLogLine(Environment.NewLine);
        }

        private bool DownloadAlbum(Album qobuzAlbum, string basePath, string albumPathSuffix = "")
        {
            bool noErrorsOccured = true;

            // Get Album model object with first batch of tracks
            const int tracksLimit = 50;
            qobuzAlbum = ExecuteApiCall(apiService => apiService.GetAlbum(qobuzAlbum.Id, true, null, tracksLimit, 0));

            // If API call failed, abort Album Download
            if (string.IsNullOrEmpty(qobuzAlbum.Id)) { return false; }

            // Get all album information and update UI fields via callback
            _downloadInfo.SetAlbumDownloadInfo(qobuzAlbum);
            Options.UpdateAlbumTagsUi.Invoke(_downloadInfo);
            _isAlbum = true;

            // Download all tracks of the Album in batches of {tracksLimit}, clean albumArt tag file after last track
            int tracksTotal = qobuzAlbum.Tracks.Total ?? 0;
            int tracksPageOffset = qobuzAlbum.Tracks.Offset ?? 0;
            int tracksLoaded = qobuzAlbum.Tracks.Items?.Count ?? 0;

            int i = 0;
            while (i < tracksLoaded)
            {
                if (State != RequestState.Running || Token.IsCancellationRequested)
                    return false;

                bool isLastTrackOfAlbum = (i + tracksPageOffset) == (tracksTotal - 1);
                Track qobuzTrack = qobuzAlbum.Tracks.Items[i];

                // Nested Album objects in Tracks are not always fully populated, inject current qobuzAlbum in Track to be downloaded
                qobuzTrack.Album = qobuzAlbum;

                if (!DownloadTrack(qobuzTrack, basePath, false, isLastTrackOfAlbum, albumPathSuffix))
                    noErrorsOccured = false;

                i++;

                if (i == tracksLoaded && tracksTotal > (i + tracksPageOffset))
                {
                    // load next page of tracks
                    tracksPageOffset += tracksLimit;
                    qobuzAlbum = ExecuteApiCall(apiService => apiService.GetAlbum(qobuzAlbum.Id, true, null, tracksLimit, tracksPageOffset));

                    // If API call failed, abort Album Download
                    if (string.IsNullOrEmpty(qobuzAlbum.Id)) { return false; }

                    // If Album Track Items is empty, Qobuz max API offset might be reached
                    if (qobuzAlbum.Tracks?.Items?.Any() != true) break;

                    // Reset counter for looping next batch of tracks
                    i = 0;
                    tracksLoaded = qobuzAlbum.Tracks.Items?.Count ?? 0;
                }
            }
            DownloadBooklets(qobuzAlbum, _downloadPaths.Path3Full);
            //await _requestContainer.Task;
            // if (!_requestContainer.Exception.InnerExceptions.Any())
            // noErrorsOccured = false;
            return noErrorsOccured;
        }

        private void DownloadBooklets(Album qobuzAlbum, string basePath)
        {
            List<Goody> booklets = qobuzAlbum.Goodies?.Where(g => g.FileFormatId == (int)GoodiesFileType.BOOKLET).ToList();

            if (booklets == null || !booklets.Any())
                return;

            _logger.AddDownloadLogLine($"Goodies found, downloading...{Environment.NewLine}", true, true);

            int counter = 1;

            foreach (Goody booklet in booklets)
            {
                string bookletFileName = counter == 1 ? "Digital Booklet.pdf" : $"Digital Booklet {counter}.pdf";
                string bookletFilePath = Path.Combine(basePath, bookletFileName);

                // Download booklet if file doesn't exist yet
                if (File.Exists(bookletFilePath))
                    _logger.AddDownloadLogLine($"Booklet file for \"{bookletFileName}\" already exists. Skipping.{Environment.NewLine}", true, true);
                else
                    _requestContainer.Add(DownloadBooklet(booklet, bookletFileName, bookletFilePath));

                counter++;
            }
        }

        private GetRequest DownloadBooklet(Goody booklet, string fileName, string filePath)
        {
            GetRequestOptions options = new()
            {
                IsDownload = true,
                Filename = fileName,
                DirectoryPath = filePath,
                NumberOfAttempts = 1,
                RequestCompleated = (req, _) => _logger.AddDownloadLogLine($"Booklet \"{fileName}\" download complete!{Environment.NewLine}", true, true),
                RequestFailed = (req, _) =>
                {
                    if (req.Exception.InnerException is AggregateException ae)
                    {
                        // When a Task fails, an AggregateException is thrown. Could be a HttpClient timeout or network error.
                        _logger.AddDownloadLogErrorLine($"Goodies Download canceled, probably due to network error or request timeout. Details saved to error log.{Environment.NewLine}", true, true);

                        _logger.AddDownloadErrorLogLine("Goodies Download canceled, probably due to network error or request timeout.");
                        _logger.AddDownloadErrorLogLine(ae.ToString());
                        _logger.AddDownloadErrorLogLine(Environment.NewLine);
                        return;
                    }
                    // If there is an unknown issue trying to, or during the download, show and log error info.
                    _logger.AddDownloadLogErrorLine($"Unknown error during Goodies Download. Details saved to error log.{Environment.NewLine}", true, true);

                    _logger.AddDownloadErrorLogLine("Unknown error during Goodies Download.");
                    _logger.AddDownloadErrorLogLine(req.Exception.ToString());
                    _logger.AddDownloadErrorLogLine(Environment.NewLine);
                }
            };
            return new GetRequest(booklet.Url, options);
        }

        private bool DownloadAlbums(string basePath, List<Album> albums, bool isEndOfDownloadJob)
        {
            bool noAlbumErrorsOccured = true;

            foreach (Album qobuzAlbum in albums)
            {
                if (State != RequestState.Running || Token.IsCancellationRequested)
                    return false;

                // Empty output, then say Starting Downloads.
                _logger.ClearUiLogComponent();
                _logger.AddEmptyDownloadLogLine(true, false);
                _logger.AddDownloadLogLine($"Starting Downloads for album \"{qobuzAlbum.Title}\" with ID: <{qobuzAlbum.Id}>...", true, true);
                _logger.AddEmptyDownloadLogLine(true, true);

                bool albumDownloadOK = DownloadAlbum(qobuzAlbum, basePath, $" [{qobuzAlbum.Id}]");

                // If album download failed, mark error occured and continue
                if (!albumDownloadOK) noAlbumErrorsOccured = false;
            }

            if (isEndOfDownloadJob)
                _logger.LogFinishedDownloadJob(noAlbumErrorsOccured);

            return noAlbumErrorsOccured;
        }

        // Convert Release to Album for download.
        private bool DownloadReleases(string basePath, List<Release> releases)
        {
            bool noAlbumErrorsOccured = true;

            foreach (Release qobuzRelease in releases)
            {
                if (State != RequestState.Running)
                    return false;

                // Fetch Album object corresponding to release
                Album qobuzAlbum = ExecuteApiCall(apiService => apiService.GetAlbum(qobuzRelease.Id, true, null, 0, 0));

                // If API call failed, mark error occured and continue with next album
                if (string.IsNullOrEmpty(qobuzAlbum.Id)) { noAlbumErrorsOccured = false; continue; }

                // Empty output, then say Starting Downloads.
                //_logger.ClearUiLogComponent();
                _logger.AddEmptyDownloadLogLine(true, false);
                _logger.AddDownloadLogLine($"Starting Downloads for album \"{qobuzAlbum.Title}\" with ID: <{qobuzAlbum.Id}>...", true, true);
                _logger.AddEmptyDownloadLogLine(true, true);

                bool albumDownloadOK = DownloadAlbum(qobuzAlbum, basePath, $" [{qobuzAlbum.Id}]");

                // If album download failed, mark error occured and continue
                if (!albumDownloadOK) noAlbumErrorsOccured = false;
            }

            return noAlbumErrorsOccured;
        }

        private bool DownloadArtistReleases(Artist qobuzArtist, string basePath, string releaseType, bool isEndOfDownloadJob)
        {
            bool noErrorsOccured = true;

            // Get ReleasesList model object with first batch of releases
            const int releasesLimit = 100;
            int releasesOffset = 0;
            ReleasesList releasesList = ExecuteApiCall(apiService => apiService.GetReleaseList(qobuzArtist.Id.ToString(), true, releaseType, "release_date", "desc", 0, releasesLimit, releasesOffset));

            // If API call failed, abort Artist Download
            if (releasesList == null) { return false; }

            bool continueDownload = true;

            while (continueDownload)
            {
                if (State != RequestState.Running)
                    return false;

                // If releases download failed, mark artist error occured and continue with next artist
                if (!DownloadReleases(basePath, releasesList.Items)) noErrorsOccured = false;

                if (releasesList.HasMore)
                {
                    // Fetch next batch of releases
                    releasesOffset += releasesLimit;
                    releasesList = ExecuteApiCall(apiService => apiService.GetReleaseList(qobuzArtist.Id.ToString(), true, releaseType, "release_date", "desc", 0, releasesLimit, releasesOffset));
                }
                else
                {
                    continueDownload = false;
                }
            }

            if (isEndOfDownloadJob)
            {
                _logger.LogFinishedDownloadJob(noErrorsOccured);
            }

            return noErrorsOccured;
        }

        // For downloading "track" links
        private async Task<bool> StartDownloadTrackTaskAsync()
        {
            // Empty screen output, then say Grabbing info.
            // _logger.ClearUiLogComponent();
            _logger.AddDownloadLogLine($"Grabbing Track info...{Environment.NewLine}", true, true);

            // Set "basePath" as the selected path.
            string downloadBasePath = Settings.Default.savedFolder;

            try
            {
                Track qobuzTrack = ExecuteApiCall(apiService => apiService.GetTrack(_downloadInfo.DownloadItemID, true));

                // If API call failed, abort
                if (qobuzTrack == null) { return ReturnFail(); }

                _logger.AddDownloadLogLine($"Track \"{qobuzTrack.Title}\" found. Starting Download...", true, true);
                _logger.AddEmptyDownloadLogLine(true, true);

                bool fileDownloaded = DownloadTrack(qobuzTrack, downloadBasePath, true, true);

                // If download failed, abort
                if (!fileDownloaded) { return ReturnFail(); }
                await _requestContainer.Task;
                // Say that downloading is completed.
                _logger.AddEmptyDownloadLogLine(true, true);
                _logger.AddDownloadLogLine("Download job completed! All downloaded files will be located in your chosen path.", true, true);
            }
            catch (Exception downloadEx)
            {
                _logger.LogDownloadTaskException("Track", downloadEx);
                return false;
            }
            return true;
        }


        // For downloading "album" links
        private async Task<bool> StartDownloadAlbumTaskAsync()
        {
            // Empty screen output, then say Grabbing info.
            // _logger.ClearUiLogComponent();
            _logger.AddDownloadLogLine($"Grabbing Album info...{Environment.NewLine}", true, true);

            // Set "basePath" as the selected path.
            String downloadBasePath = Settings.Default.savedFolder;

            try
            {
                // Get Album model object without tracks (tracks are loaded in batches later)
                Album qobuzAlbum = ExecuteApiCall(apiService => apiService.GetAlbum(_downloadInfo.DownloadItemID, true, null, 0));

                // If API call failed, abort
                if (qobuzAlbum == null) { return ReturnFail(); }

                _logger.AddDownloadLogLine($"Album \"{qobuzAlbum.Title}\" found. Starting Downloads...", true, true);
                _logger.AddEmptyDownloadLogLine(true, true);
                bool finished = DownloadAlbum(qobuzAlbum, downloadBasePath);
                await _requestContainer.Task;
                _logger.LogFinishedDownloadJob(finished);
            }
            catch (Exception downloadEx)
            {
                _logger.LogDownloadTaskException("Album", downloadEx);
                return false;
            }
            return true;
        }

        // For downloading "artist" links
        private async Task<bool> StartDownloadArtistDiscogTaskAsync()
        {
            // Set "basePath" as the selected path.
            String artistBasePath = Settings.Default.savedFolder;

            // Empty output, then say Grabbing IDs.
            // logger.ClearUiLogComponent();
            _logger.AddDownloadLogLine("Grabbing Artist info...", true, true);

            try
            {
                // Get Artist model object
                Artist qobuzArtist = ExecuteApiCall(apiService => apiService.GetArtist(_downloadInfo.DownloadItemID, true));

                // If API call failed, abort
                if (qobuzArtist == null) { return ReturnFail(); }

                _logger.AddDownloadLogLine($"Starting Downloads for artist \"{qobuzArtist.Name}\" with ID: <{qobuzArtist.Id}>...", true, true);

                DownloadArtistReleases(qobuzArtist, artistBasePath, "all", true);
            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Artist", downloadEx);
                return false;
            }
            return true;
        }

        // For downloading "label" links
        private async Task<bool> StartDownloadLabelTaskAsync()
        {
            // Set "basePath" as the selected path + "/- Labels".
            string labelBasePath = Path.Combine(Settings.Default.savedFolder, "- Labels");

            // Empty output, then say Grabbing IDs.
            // logger.ClearUiLogComponent();
            _logger.AddDownloadLogLine("Grabbing Label albums...", true, true);

            try
            {
                // Initialise full Album list
                Label qobuzLabel = null;
                List<Album> labelAlbums = new();
                const int albumLimit = 500;
                int albumsOffset = 0;

                while (true)
                {
                    // Get Label model object with albums
                    qobuzLabel = ExecuteApiCall(apiService => apiService.GetLabel(_downloadInfo.DownloadItemID, true, "albums", albumLimit, albumsOffset));

                    // If API call failed, abort
                    if (qobuzLabel == null) { return ReturnFail(); }

                    // If resulting Label has no Album Items, Qobuz API maximum offset is reached
                    if (qobuzLabel.Albums?.Items?.Any() != true) break;

                    labelAlbums.AddRange(qobuzLabel.Albums.Items);

                    // Exit loop when all albums are loaded or the Qobuz imposed limit of 10000 is reached
                    if ((qobuzLabel.Albums?.Total ?? 0) == labelAlbums.Count) break;

                    albumsOffset += albumLimit;
                }

                // If label has no albums, log and abort
                if (!labelAlbums.Any())
                {
                    _logger.AddDownloadLogLine($"No albums found for label \"{qobuzLabel.Name}\" with ID: <{qobuzLabel.Id}>, nothing to download.", true, true);
                    return ReturnFail();
                }

                _logger.AddDownloadLogLine($"Starting Downloads for label \"{qobuzLabel.Name}\" with ID: <{qobuzLabel.Id}>...", true, true);

                // Add Label name to basePath
                string safeLabelName = StringTools.GetSafeFilename(StringTools.DecodeEncodedNonAsciiCharacters(qobuzLabel.Name));
                labelBasePath = Path.Combine(labelBasePath, safeLabelName);

                DownloadAlbums(labelBasePath, labelAlbums, true);
            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Label", downloadEx);
                return false;
            }
            return true;
        }


        // For downloading "favorites"

        // Favorite Albums
        private async Task<bool> StartDownloadFaveAlbumsTaskAsync()
        {
            // Set "basePath" as the selected path + "/- Favorites".
            string favoritesBasePath = Path.Combine(Settings.Default.savedFolder, "- Favorites");

            // Empty output, then say Grabbing IDs.
            //_logger.ClearUiLogComponent();
            _logger.AddDownloadLogLine("Grabbing Favorite Albums...", true, true);

            try
            {
                // Initialise full Album list
                List<Album> favoriteAlbums = new();
                const int albumLimit = 500;
                int albumsOffset = 0;

                while (true)
                {
                    // Get UserFavorites model object with albums
                    UserFavorites qobuzUserFavorites = ExecuteApiCall(apiService => apiService.GetUserFavorites(_downloadInfo.DownloadItemID, "albums", albumLimit, albumsOffset));

                    // If API call failed, abort
                    if (qobuzUserFavorites == null) { return ReturnFail(); }

                    // If resulting UserFavorites has no Album Items, Qobuz API maximum offset is reached
                    if (qobuzUserFavorites.Albums?.Items?.Any() != true) break;

                    favoriteAlbums.AddRange(qobuzUserFavorites.Albums.Items);

                    // Exit loop when all albums are loaded
                    if ((qobuzUserFavorites.Albums?.Total ?? 0) == favoriteAlbums.Count) break;

                    albumsOffset += albumLimit;
                }

                // If user has no favorite albums, log and abort
                if (!favoriteAlbums.Any())
                {
                    _logger.AddDownloadLogLine("No favorite albums found, nothing to download.", true, true);
                    return ReturnFail();
                }

                // Download all favorite albums
                DownloadAlbums(favoritesBasePath, favoriteAlbums, true);
            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Favorite Albums", downloadEx);
                return false;
            }
            return true;
        }

        // Favorite Artists
        private async Task<bool> StartDownloadFaveArtistsTaskAsync()
        {
            // Set "basePath" as the selected path + "/- Favorites".
            string favoritesBasePath = Path.Combine(Settings.Default.savedFolder, "- Favorites");

            // Empty output, then say Grabbing IDs.
            //_logger.ClearUiLogComponent();
            _logger.AddDownloadLogLine("Grabbing Favorite Artists...", true, true);

            try
            {
                bool noArtistErrorsOccured = true;

                // Get UserFavoritesIds model object, getting Id's allows all results at once.
                UserFavoritesIds qobuzUserFavoritesIds = ExecuteApiCall(apiService => apiService.GetUserFavoriteIds(_downloadInfo.DownloadItemID));

                // If API call failed, abort
                if (qobuzUserFavoritesIds == null) { return ReturnFail(); }

                // If user has no favorite artists, log and abort
                if (qobuzUserFavoritesIds.Artists?.Any() != true)
                {
                    _logger.AddDownloadLogLine("No favorite artists found, nothing to download.", true, true);
                    return ReturnFail();
                }

                // Download favorite artists
                foreach (int favoriteArtistId in qobuzUserFavoritesIds.Artists)
                {
                    if (State != RequestState.Running)
                        return false;

                    // Get Artist model object
                    Artist qobuzArtist = ExecuteApiCall(apiService => apiService.GetArtist(favoriteArtistId.ToString(), true));

                    // If API call failed, mark artist error occured and continue with next artist
                    if (qobuzArtist == null) { noArtistErrorsOccured = false; continue; }

                    _logger.AddEmptyDownloadLogLine(true, true);
                    _logger.AddDownloadLogLine($"Starting Downloads for artist \"{qobuzArtist.Name}\" with ID: <{qobuzArtist.Id}>...", true, true);

                    // If albums download failed, mark artist error occured and continue with next artist
                    if (!DownloadArtistReleases(qobuzArtist, favoritesBasePath, "all", false)) noArtistErrorsOccured = false;
                }

                _logger.LogFinishedDownloadJob(noArtistErrorsOccured);
            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Favorite Albums", downloadEx);
                return false;
            }
            return true;
        }

        // Favorite Tracks
        private async Task<bool> StartDownloadFaveTracksTaskAsync()
        {
            // Set "basePath" as the selected path + "/- Favorites".
            string favoriteTracksBasePath = Path.Combine(Settings.Default.savedFolder, "- Favorites");

            // Empty screen output, then say Grabbing info.
            //_logger.ClearUiLogComponent();
            _logger.AddDownloadLogLine("Grabbing Favorite Tracks...", true, true);
            _logger.AddEmptyDownloadLogLine(true, true);

            try
            {
                bool noTrackErrorsOccured = true;

                // Get UserFavoritesIds model object, getting Id's allows all results at once.
                UserFavoritesIds qobuzUserFavoritesIds = ExecuteApiCall(apiService => apiService.GetUserFavoriteIds(_downloadInfo.DownloadItemID));

                // If API call failed, abort
                if (qobuzUserFavoritesIds == null) { return ReturnFail(); }

                // If user has no favorite tracks, log and abort
                if (qobuzUserFavoritesIds.Tracks?.Any() != true)
                {
                    _logger.AddDownloadLogLine("No favorite tracks found, nothing to download.", true, true);
                    return ReturnFail();
                }

                _logger.AddDownloadLogLine("Favorite tracks found. Starting Downloads...", true, true);
                _logger.AddEmptyDownloadLogLine(true, true);

                // Download favorite tracks
                foreach (int favoriteTrackId in qobuzUserFavoritesIds.Tracks)
                {
                    // User requested task cancellation!
                    if (State != RequestState.Running)
                        return false;

                    Track qobuzTrack = ExecuteApiCall(apiService => apiService.GetTrack(favoriteTrackId.ToString(), true));

                    // If API call failed, log and continue with next track
                    if (qobuzTrack == null) { noTrackErrorsOccured = false; continue; }

                    if (!DownloadTrack(qobuzTrack, favoriteTracksBasePath, true, true)) noTrackErrorsOccured = false;
                }

                _logger.LogFinishedDownloadJob(noTrackErrorsOccured);
            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Playlist", downloadEx);
                return false;
            }
            return true;
        }

        // For downloading "playlist" links
        private async Task<bool> StartDownloadPlaylistTaskAsync()
        {
            // Set "basePath" as the selected path.
            String playlistBasePath = Settings.Default.savedFolder;

            // Empty screen output, then say Grabbing info.
            //_logger.ClearUiLogComponent();
            _logger.AddDownloadLogLine("Grabbing Playlist tracks...", true, true);
            _logger.AddEmptyDownloadLogLine(true, true);

            try
            {
                // Get Playlist model object with all track_ids
                Playlist qobuzPlaylist = ExecuteApiCall(apiService => apiService.GetPlaylist(_downloadInfo.DownloadItemID, true, "track_ids", 10000));

                // If API call failed, abort
                if (qobuzPlaylist == null) { return ReturnFail(); }

                // If playlist empty, log and abort
                if (qobuzPlaylist.TrackIds?.Any() != true)
                {
                    _logger.AddDownloadLogLine($"Playlist \"{qobuzPlaylist.Name}\" is empty, nothing to download.", true, true);
                    return ReturnFail();
                }

                _logger.AddDownloadLogLine($"Playlist \"{qobuzPlaylist.Name}\" found. Starting Downloads...", true, true);
                _logger.AddEmptyDownloadLogLine(true, true);

                // Create Playlist root directory.
                string playlistSafeName = StringTools.GetSafeFilename(StringTools.DecodeEncodedNonAsciiCharacters(qobuzPlaylist.Name));
                string playlistNamePath = StringTools.TrimToMaxLength(playlistSafeName, Globals.MaxLength);
                playlistBasePath = Path.Combine(playlistBasePath, "- Playlists", playlistNamePath);
                Directory.CreateDirectory(playlistBasePath);

                // Download Playlist cover art to "Playlist.jpg" in root directory (if not exists)
                string coverArtFilePath = Path.Combine(playlistBasePath, "Playlist.jpg");

                if (!File.Exists(coverArtFilePath))
                {

                    _requestContainer.Add(new GetRequest(qobuzPlaylist.ImageRectangle.FirstOrDefault<string>(), new()
                    {
                        DirectoryPath = playlistBasePath,
                        Filename = "Playlist.jpg",
                        IsDownload = true,
                        RequestFailed = (req, _) => _logger.AddDownloadErrorLogLines(["Error downloading full size playlist cover image file.", req.Exception.Message, "\r\n"])
                    }));
                }

                bool noTrackErrorsOccured = true;

                // Start new m3u Playlist file.
                M3uPlaylist m3uPlaylist = new();
                m3uPlaylist.IsExtended = true;

                // Download Playlist tracks
                foreach (long trackId in qobuzPlaylist.TrackIds)
                {
                    // User requested task cancellation!
                    if (State != RequestState.Running)
                        return false;

                    // Fetch full Track info
                    Track qobuzTrack = ExecuteApiCall(apiService => apiService.GetTrack(trackId.ToString(), true));

                    // If API call failed, log and continue with next track
                    if (qobuzTrack == null) { noTrackErrorsOccured = false; continue; }

                    if (!IsStreamable(qobuzTrack, true)) continue;

                    if (!DownloadTrack(qobuzTrack, playlistBasePath, true, true)) noTrackErrorsOccured = false;

                    AddTrackToPlaylistFile(m3uPlaylist, _downloadInfo, _downloadPaths);
                }

                // Write m3u playlist to file, override if exists
                string m3uPlaylistFile = Path.Combine(playlistBasePath, $"{playlistSafeName}.m3u8");
                File.WriteAllText(m3uPlaylistFile, PlaylistToTextHelper.ToText(m3uPlaylist), System.Text.Encoding.UTF8);

                _logger.LogFinishedDownloadJob(noTrackErrorsOccured);

            }
            catch (Exception downloadEx)
            {
                _logger.ClearUiLogComponent();
                _logger.LogDownloadTaskException("Playlist", downloadEx);
                return false;
            }
            return true;
        }

        public void AddTrackToPlaylistFile(M3uPlaylist m3uPlaylist, DownloadItemInfo downloadInfo, DownloadItemPaths downloadPaths)
        {
            // If the TrackFile doesn't exist, skip.
            if (!File.Exists(downloadPaths.FullTrackFilePath)) return;

            // Add successfully downloaded file to m3u playlist
            m3uPlaylist.PlaylistEntries.Add(new M3uPlaylistEntry()
            {
                Path = downloadPaths.FullTrackFilePath,
                Duration = TimeSpan.FromSeconds(_downloadInfo.Duration),
                Title = $"{downloadInfo.PerformerName} - {downloadInfo.TrackName}"
            });
        }

        public void CreateTrackDirectories(string basePath, string qualityPath, string albumPathSuffix = "", bool forTracklist = false)
        {
            if (forTracklist)
            {
                _downloadPaths.Path1Full = basePath;
                _downloadPaths.Path2Full = _downloadPaths.Path1Full;
                _downloadPaths.Path3Full = Path.Combine(basePath, qualityPath);
                _downloadPaths.Path4Full = _downloadPaths.Path3Full;
            }
            else
            {
                _downloadPaths.Path1Full = Path.Combine(basePath, _downloadPaths.AlbumArtistPath);
                _downloadPaths.Path2Full = Path.Combine(basePath, _downloadPaths.AlbumArtistPath, _downloadPaths.AlbumNamePath + albumPathSuffix);
                _downloadPaths.Path3Full = Path.Combine(basePath, _downloadPaths.AlbumArtistPath, _downloadPaths.AlbumNamePath + albumPathSuffix, qualityPath);

                // If more than 1 disc, create folders for discs. Otherwise, strings will remain null
                // Pad discnumber with minimum of 2 integer positions based on total number of disks
                if (_downloadInfo.DiscTotal > 1)
                {
                    // Create strings for disc folders
                    string discFolder = "CD " + _downloadInfo.DiscNumber.ToString().PadLeft(Math.Max(2, (int)Math.Floor(Math.Log10(_downloadInfo.DiscTotal) + 1)), '0');
                    _downloadPaths.Path4Full = Path.Combine(basePath, _downloadPaths.AlbumArtistPath, _downloadPaths.AlbumNamePath + albumPathSuffix, qualityPath, discFolder);
                }
                else
                {
                    _downloadPaths.Path4Full = _downloadPaths.Path3Full;
                }
            }

            System.IO.Directory.CreateDirectory(_downloadPaths.Path4Full);
        }

        private bool ReturnFail()
        {
            Options.NumberOfAttempts = 0;
            return false;
        }
    }
}
