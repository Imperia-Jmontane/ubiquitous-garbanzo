using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using MyApp.Application.Abstractions;

namespace MyApp.Controllers.Api
{
    [ApiController]
    [Route("api/repositories/clone")]
    public sealed class RepositoryCloneController : ControllerBase
    {
        private readonly IRepositoryCloneCoordinator _cloneCoordinator;

        public RepositoryCloneController(IRepositoryCloneCoordinator cloneCoordinator)
        {
            if (cloneCoordinator == null)
            {
                throw new ArgumentNullException(nameof(cloneCoordinator));
            }

            _cloneCoordinator = cloneCoordinator;
        }

        [HttpPost]
        public ActionResult<QueueCloneResponse> QueueClone([FromBody] QueueCloneRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RepositoryUrl))
            {
                QueueCloneErrorResponse errorResponse = new QueueCloneErrorResponse
                {
                    Message = "The repository URL must be provided."
                };
                return BadRequest(errorResponse);
            }

            RepositoryCloneTicket ticket;

            try
            {
                ticket = _cloneCoordinator.QueueClone(request.RepositoryUrl);
            }
            catch (ArgumentException exception)
            {
                QueueCloneErrorResponse errorResponse = new QueueCloneErrorResponse
                {
                    Message = exception.Message
                };
                return BadRequest(errorResponse);
            }

            QueueCloneResponse response = new QueueCloneResponse
            {
                OperationId = ticket.OperationId,
                AlreadyCloned = ticket.AlreadyCloned,
                Enqueued = ticket.Enqueued,
                Notification = ticket.AlreadyCloned
                    ? string.Format("Repository already cloned: {0}", request.RepositoryUrl)
                    : (!ticket.Enqueued && ticket.HasOperation
                        ? string.Format("Repository clone already in progress: {0}", request.RepositoryUrl)
                        : request.RepositoryUrl),
                RepositoryUrl = request.RepositoryUrl
            };

            if (ticket.HasOperation)
            {
                RepositoryCloneStatus? status;

                if (_cloneCoordinator.TryGetStatus(ticket.OperationId, out status) && status != null)
                {
                    response.Status = CreateStatusResponse(status);
                }
            }
            else if (ticket.AlreadyCloned)
            {
                response.Status = new RepositoryCloneStatusResponse
                {
                    OperationId = Guid.Empty,
                    RepositoryUrl = request.RepositoryUrl,
                    State = RepositoryCloneState.Completed.ToString(),
                    Percentage = 100.0,
                    Stage = "Completed",
                    Message = "Repository already cloned.",
                    LastUpdatedUtc = DateTimeOffset.UtcNow
                };
            }

            if (ticket.AlreadyCloned && !ticket.HasOperation)
            {
                return Ok(response);
            }

            return Accepted(response);
        }

        [HttpGet]
        public ActionResult<IReadOnlyCollection<RepositoryCloneStatusResponse>> GetActiveClones()
        {
            IReadOnlyCollection<RepositoryCloneStatus> statuses = _cloneCoordinator.GetActiveClones();
            List<RepositoryCloneStatusResponse> responses = new List<RepositoryCloneStatusResponse>();

            foreach (RepositoryCloneStatus status in statuses)
            {
                responses.Add(CreateStatusResponse(status));
            }

            return Ok(responses);
        }

        [HttpGet("{operationId:guid}")]
        public ActionResult<RepositoryCloneStatusResponse> GetCloneStatus(Guid operationId)
        {
            RepositoryCloneStatus? status;

            if (!_cloneCoordinator.TryGetStatus(operationId, out status) || status == null)
            {
                return NotFound();
            }

            RepositoryCloneStatusResponse response = CreateStatusResponse(status);
            return Ok(response);
        }

        [HttpDelete("{operationId:guid}")]
        public ActionResult<RepositoryCloneStatusResponse> CancelClone(Guid operationId)
        {
            bool canceled = _cloneCoordinator.CancelClone(operationId);

            if (!canceled)
            {
                return NotFound();
            }

            RepositoryCloneStatus? status;

            if (_cloneCoordinator.TryGetStatus(operationId, out status) && status != null)
            {
                RepositoryCloneStatusResponse response = CreateStatusResponse(status);
                return Accepted(response);
            }

            return Accepted();
        }

        private static RepositoryCloneStatusResponse CreateStatusResponse(RepositoryCloneStatus status)
        {
            return new RepositoryCloneStatusResponse
            {
                OperationId = status.OperationId,
                RepositoryUrl = status.RepositoryUrl,
                State = status.State.ToString(),
                Percentage = status.Percentage,
                Stage = status.Stage,
                Message = status.Message,
                LastUpdatedUtc = status.LastUpdatedUtc
            };
        }

        public sealed class QueueCloneRequest
        {
            public string RepositoryUrl { get; set; } = string.Empty;
        }

        public sealed class QueueCloneResponse
        {
            public Guid OperationId { get; set; }

            public bool AlreadyCloned { get; set; }

            public bool Enqueued { get; set; }

            public string Notification { get; set; } = string.Empty;

            public string RepositoryUrl { get; set; } = string.Empty;

            public RepositoryCloneStatusResponse? Status { get; set; }
        }

        public sealed class QueueCloneErrorResponse
        {
            public string Message { get; set; } = string.Empty;
        }

        public sealed class RepositoryCloneStatusResponse
        {
            public Guid OperationId { get; set; }

            public string RepositoryUrl { get; set; } = string.Empty;

            public string State { get; set; } = string.Empty;

            public double Percentage { get; set; }

            public string Stage { get; set; } = string.Empty;

            public string Message { get; set; } = string.Empty;

            public DateTimeOffset LastUpdatedUtc { get; set; }
        }
    }
}
