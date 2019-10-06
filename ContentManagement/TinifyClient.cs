using System;
using System.Configuration;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TinifyAPI;
using Exception = System.Exception;

namespace ContentManagement
{
    internal class TinifyClient
    {
        private static readonly string[] ApiKeys = ConfigurationManager.AppSettings["TinifyApiKeys"]
            .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        private static readonly int CompressionsLimit = int.Parse(ConfigurationManager.AppSettings["TinifyCompressionsLimit"]);
        private static readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1);

        private static int _activeKeyIndex;
        private static long _remainingRequests;
        private static int _pendingTasks;

        public static readonly AutoResetEvent AllDone = new AutoResetEvent(true);

        public static async Task<byte[]> Fit(byte[] image, int width, int height)
        {
            try
            {
                IncrementPending();
                await SemaphoreSlim.WaitAsync().ConfigureAwait(false);

                Task<Source> resized;
                try
                {
                    await SetApiKey().ConfigureAwait(false);
                    var source = Tinify.FromBuffer(image);
                    resized = source.Resize(new { method = "fit", width, height });
                }
                finally
                {
                    SemaphoreSlim.Release();
                }
				Debug.WriteLine("Sending compression request");
                var result = await resized.ToBuffer().ConfigureAwait(false);

                return result;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                throw;
            }
            finally
            {
                DecrementPending();
            }
        }

        private static async Task SetApiKey()
        {
            while (_activeKeyIndex < ApiKeys.Length)
            {
                if (--_remainingRequests >= 0)
                {
	                Debug.WriteLine($"Existing key used. Remaining compressions count {_remainingRequests}");
                    return;
                }

                Tinify.Key = ApiKeys[_activeKeyIndex++];

                if (!await Tinify.Validate().ConfigureAwait(false) || Tinify.CompressionCount == null)
                {
                    continue;
                }

                _remainingRequests = CompressionsLimit - Tinify.CompressionCount.Value;

                if (--_remainingRequests >= 0)
                {
					Debug.WriteLine($"New key set. Remaining compressions count {_remainingRequests}");
                    return;
                }
            }

            throw new Exception("The limit of Tinify compressions has been reached. Please add more API keys");
        }

        private static void IncrementPending()
        {
            Interlocked.Increment(ref _pendingTasks);
            AllDone.Reset();
            Debug.WriteLine($"Pending requests {_pendingTasks}.");
        }

        private static void DecrementPending()
        {
            if (Interlocked.Decrement(ref _pendingTasks) == 0)
            {
                AllDone.Set();
            }
            Debug.WriteLine($"Pending requests {_pendingTasks}.");
        }
    }
}
