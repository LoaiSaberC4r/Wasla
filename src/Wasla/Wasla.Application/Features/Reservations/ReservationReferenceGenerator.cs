using System.Security.Cryptography;

namespace Wasla.Application.Features.Reservations;

public interface IReservationReferenceGenerator
{
    string Generate();
}

internal sealed class ReservationReferenceGenerator : IReservationReferenceGenerator
{
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    public string Generate()
    {
        Span<char> suffix = stackalloc char[6];
        for (var index = 0; index < suffix.Length; index++)
        {
            suffix[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return $"WSL-R-{suffix}";
    }
}
