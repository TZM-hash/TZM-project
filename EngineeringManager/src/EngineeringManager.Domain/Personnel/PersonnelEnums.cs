namespace EngineeringManager.Domain.Personnel;

public enum PersonnelScope
{
    Internal = 1,
    External = 2
}

public enum ExternalPersonnelType
{
    ConstructionCrew = 1,
    BusinessPartner = 2,
    Other = 3
}

public enum InternalPersonnelType
{
    Formal = 1,
    Labor = 2,
    Temporary = 3
}
