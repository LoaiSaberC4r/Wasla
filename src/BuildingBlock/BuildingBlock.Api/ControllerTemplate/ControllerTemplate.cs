using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BuildingBlock.Api.ControllerTemplate
{
    [ApiController]
    public abstract class ControllerTemplate : ControllerBase
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Design",
            "CA1051:Do not declare visible instance fields",
            Justification = "The sender field is existing controller-template surface and is preserved for compatibility.")]
        public readonly ISender sender;

        protected ControllerTemplate(ISender sender)
        {
            this.sender = sender;
        }
    }
}
