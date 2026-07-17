namespace EngineeringManager.Application.DataViews;

public interface ISavedDataViewService
{
    Task<IReadOnlyList<SavedDataViewDto>> ListAsync(string userId, DataViewDefinition definition, CancellationToken token);
    Task<SavedDataViewDto> SaveAsync(string userId, SaveDataViewRequest request, DataViewDefinition definition, CancellationToken token);
    Task DeleteAsync(string userId, Guid id, CancellationToken token);
}
