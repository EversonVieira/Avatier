using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Avatier.Api.Configuration
{
    public class ApiResponseConventions : IActionModelConvention
    {
        public void Apply(ActionModel action)
        {
            action.Filters.Add(new ProducesResponseTypeAttribute<ProblemDetails>(StatusCodes.Status400BadRequest));

            action.Filters.Add(new ProducesResponseTypeAttribute<ProblemDetails>(StatusCodes.Status500InternalServerError));

            action.Filters.Add(new ProducesResponseTypeAttribute(StatusCodes.Status401Unauthorized));

        }
    }
}
