namespace UsersService.Models;

public class User
{
    public User()
    {
        Id = Guid.NewGuid();
    }

    public Guid Id { get; }

    public string Name { get; set; }

    public string Email { get; init; } = "alif@alif.tj";
    
    public decimal Balance { get; set; } = 100;
}