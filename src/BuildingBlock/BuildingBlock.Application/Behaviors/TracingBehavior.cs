using MediatR;
using BuildingBlock.Application.Diagnostics;
using System.Diagnostics;

namespace BuildingBlock.Application.Behaviors
{
    internal sealed class TracingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var requestType = typeof(TRequest);
            var requestKind = BuildingBlockDiagnostics.GetRequestKind(requestType);
            var resultStatus = "success";
            var stopwatch = Stopwatch.StartNew();

            using var activity = BuildingBlockDiagnostics.ActivitySource.StartActivity(
                "buildingblock.request",
                ActivityKind.Internal);

            activity?.SetTag("buildingblock.request.type", requestType.FullName);
            activity?.SetTag("buildingblock.request.name", requestType.Name);
            activity?.SetTag("buildingblock.request.kind", requestKind);

            try
            {
                var response = await next(cancellationToken);
                activity?.SetTag("buildingblock.response.type", typeof(TResponse).FullName);

                if (ResultDiagnostics.TryInspect(response, out var result) && result.IsFailure)
                {
                    resultStatus = "failure";
                    var primary = result.PrimaryError;

                    activity?.SetTag("buildingblock.result.status", resultStatus);
                    activity?.SetTag("buildingblock.error.count", result.ErrorCount);
                    activity?.SetTag("buildingblock.error.code", primary?.Code);
                    activity?.SetTag("buildingblock.error.type", primary?.Type.ToString());
                    activity?.SetStatus(ActivityStatusCode.Error);
                }
                else
                {
                    activity?.SetTag("buildingblock.result.status", resultStatus);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                }

                return response;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                resultStatus = "cancelled";
                activity?.SetTag("buildingblock.result.status", resultStatus);
                throw;
            }
            catch (Exception exception)
            {
                resultStatus = "failure";
                activity?.SetTag("buildingblock.result.status", resultStatus);
                activity?.SetTag("buildingblock.error.type", exception.GetType().Name);
                activity?.SetStatus(ActivityStatusCode.Error);
                activity?.AddEvent(new ActivityEvent(
                    "exception",
                    tags: new ActivityTagsCollection
                    {
                        ["exception.type"] = exception.GetType().FullName,
                        ["exception.escaped"] = true
                    }));
                throw;
            }
            finally
            {
                stopwatch.Stop();
                BuildingBlockDiagnostics.RecordRequest(
                    requestKind,
                    resultStatus,
                    stopwatch.Elapsed.TotalMilliseconds);
            }
        }
    }
}
