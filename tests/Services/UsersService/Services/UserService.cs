using UsersService.Repositories;

namespace UsersService.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _repository;

    public UserService(IUserRepository _repository)
    {
        this._repository = _repository;
    }
}