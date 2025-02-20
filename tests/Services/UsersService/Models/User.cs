namespace UsersService.Models;

public class User
{
    public User()
    {
        Id = Guid.NewGuid();
    }

    public Guid Id { get; }

    public string Name { get; set; }
}