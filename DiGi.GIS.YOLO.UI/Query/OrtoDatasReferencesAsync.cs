using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.WebAPI.Classes;
using DiGi.WebAPI.Classes;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.YOLO.UI
{
    public static partial class Query
    {
        /// <summary>
        /// Reads the orthophoto listing of a county - one entry per building that has stored imagery.
        /// <para>The listing is what a prediction run splits into chunks before any image is fetched, so it is read once per county rather than once per chunk (DiGi-Organization/DiGi.GIS.YOLO.UI#30).</para>
        /// </summary>
        /// <param name="gisWebAPIManager">The <see cref="GISWebAPIManager"/> instance used to communicate with the WebAPI.</param>
        /// <param name="countyId">The identifier of the county part to list.</param>
        /// <param name="postOptions">Optional configuration options for the request.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task returning the listing - empty for a county with no imagery - or null when it could not be read.</returns>
        public static async Task<List<OrtoDatasReference>?> OrtoDatasReferencesAsync(this GISWebAPIManager? gisWebAPIManager, int countyId, PostOptions? postOptions = null, CancellationToken cancellationToken = default)
        {
            if (gisWebAPIManager is null || countyId <= 0)
            {
                return null;
            }

            HttpClient? httpClient = gisWebAPIManager.CreateHttpClient<OrtoDatasController>(nameof(OrtoDatasController.GetOrtoDatasReferencesByCountyIdAsync), out string? path);
            if (httpClient is null || string.IsNullOrWhiteSpace(path))
            {
                Serilog.Modify.Log(Serilog.Enums.LogEventLevel.Error, "HttpClient or path for GetOrtoDatasReferencesByCountyIdAsync could not be resolved");
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            string requestUri = new UrlBuilder(path).AddParameter("countyid", countyId).ToString();

            PostResponse<List<OrtoDatasReference>?> postResponse;
            try
            {
                postResponse = await DiGi.WebAPI.Query.GetAsync<List<OrtoDatasReference>>(httpClient, requestUri, postOptions ?? new PostOptions() { RequestResult = true });
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                Serilog.Modify.Log(exception, "Failed to retrieve OrtoDatasReference list for county {CountyId}", countyId);
                return null;
            }

            if (postResponse is null || !postResponse.Succeeded || postResponse.Result is not List<OrtoDatasReference> ortoDatasReferences)
            {
                Serilog.Modify.Log(Serilog.Enums.LogEventLevel.Error, "OrtoDatasReferences query returned unsuccessful result for county {CountyId}", countyId);
                return null;
            }

            return ortoDatasReferences;
        }
    }
}
