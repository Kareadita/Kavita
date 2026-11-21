using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Images;
using NetVips;

namespace Kavita.Services.Images.ImageSteps;

public record FitWithinStep() : IImageStep
{
    public Image Apply(Image image)
    {
        throw new System.NotImplementedException();
    }
}
