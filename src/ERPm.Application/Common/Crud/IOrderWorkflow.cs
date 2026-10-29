namespace ERPm.Application.Common.Crud;
public interface IOrderWorkflow<TDto>
{
    Task<TDto> ConfirmAsync(int id, CancellationToken cancellationToken);
}
