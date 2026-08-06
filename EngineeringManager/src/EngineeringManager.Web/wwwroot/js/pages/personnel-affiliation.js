const splitIds = (value) => (value || '').split(',').map((item) => item.trim()).filter(Boolean);

const optionMatchesOwner = (option, legalEntityId, businessPartnerId) => {
  if (!option.value) return true;
  const legalEntityIds = splitIds(option.dataset.legalEntityId);
  const businessPartnerIds = splitIds(option.dataset.businessPartnerId);
  if (legalEntityId) return legalEntityIds.includes(legalEntityId);
  if (businessPartnerId) return businessPartnerIds.includes(businessPartnerId);
  return false;
};

const filterOwnerSelect = (select, scope, requireCrewOwner = false) => {
  if (!select) return;
  let selectedStillValid = !select.value;
  for (const option of select.options) {
    const ownerScope = option.dataset.ownerScope;
    const matchesScope = !option.value || !scope || !ownerScope || ownerScope === scope;
    const matchesCrewType = !option.value || !requireCrewOwner || option.dataset.role === 'crew';
    const matches = matchesScope && matchesCrewType;
    option.hidden = !matches;
    option.disabled = !matches;
    if (option.selected && matches) selectedStillValid = true;
  }
  if (!selectedStillValid) select.value = '';
};

const synchronizeTypeSelect = (typeSelect, isActive) => {
  if (!typeSelect) return;
  typeSelect.disabled = !isActive;
  const field = typeSelect.closest('label');
  if (field) field.hidden = !isActive;
  if (!isActive) {
    typeSelect.value = '';
  } else if (!typeSelect.value) {
    typeSelect.value = typeSelect.dataset.defaultValue || '';
  }
};

const filterSelect = (select, legalEntityId, businessPartnerId) => {
  if (!select) return;
  let selectedStillValid = !select.value;
  for (const option of select.options) {
    const matchesOwner = optionMatchesOwner(option, legalEntityId, businessPartnerId);
    option.hidden = !matchesOwner;
    option.disabled = !matchesOwner;
    if (option.selected && matchesOwner) selectedStillValid = true;
  }
  if (!selectedStillValid) select.value = '';
};

const filterCrewSelect = (crewSelect, selectedProject, forcedCrewId = '') => {
  if (!crewSelect) return;
  const projectBusinessPartnerIds = splitIds(selectedProject ? selectedProject.dataset.businessPartnerId : '');
  const hasProject = Boolean(selectedProject?.value);
  let selectedStillValid = !crewSelect.value;
  for (const option of crewSelect.options) {
    const matchesForcedCrew = !option.value || !forcedCrewId || option.value === forcedCrewId;
    const matchesProject = !option.value || !hasProject || projectBusinessPartnerIds.includes(option.value);
    const matches = matchesForcedCrew && matchesProject;
    option.hidden = !matches;
    option.disabled = !matches;
    if (option.selected && matches) selectedStillValid = true;
  }
  if (forcedCrewId) {
    crewSelect.value = forcedCrewId;
    crewSelect.dataset.forcedByOwner = 'true';
  } else {
    delete crewSelect.dataset.forcedByOwner;
    if (!selectedStillValid) crewSelect.value = '';
  }
  crewSelect.disabled = Boolean(forcedCrewId);
};

const initializeAffiliationEditor = (root) => {
  const scopeSelect = root.querySelector('[data-affiliation-scope]');
  const internalTypeSelect = root.querySelector('[data-affiliation-internal-type]');
  const externalTypeSelect = root.querySelector('[data-affiliation-external-type]');
  const ownerSelect = root.querySelector('[data-affiliation-owner]');
  const departmentSelect = root.querySelector('[data-affiliation-department]');
  const projectSelect = root.querySelector('[data-affiliation-project]');
  const crewSelect = root.querySelector('[data-affiliation-crew]');
  const legalEntityInput = root.querySelector('[data-affiliation-legal-entity]');
  const businessPartnerInput = root.querySelector('[data-affiliation-business-partner]');
  if (!ownerSelect || !legalEntityInput || !businessPartnerInput) return;

  const synchronize = ({ clearDependents = false } = {}) => {
    const scope = scopeSelect ? scopeSelect.value : root.dataset.personnelScope || '';
    synchronizeTypeSelect(internalTypeSelect, scope === 'Internal');
    synchronizeTypeSelect(externalTypeSelect, scope === 'External');
    const externalType = externalTypeSelect?.value || root.dataset.personnelExternalType || '';
    const requiresCrewOwner = scope === 'External' && externalType === 'ConstructionCrew';
    filterOwnerSelect(ownerSelect, scope, requiresCrewOwner);
    const selectedOwner = ownerSelect.selectedOptions[0];
    const legalEntityId = selectedOwner ? selectedOwner.dataset.legalEntityId || '' : '';
    const businessPartnerId = selectedOwner ? selectedOwner.dataset.businessPartnerId || '' : '';
    legalEntityInput.value = legalEntityId;
    businessPartnerInput.value = businessPartnerId;

    if (clearDependents) {
      if (departmentSelect) departmentSelect.value = '';
      if (projectSelect) projectSelect.value = '';
      if (crewSelect) crewSelect.value = '';
    }

    filterSelect(departmentSelect, legalEntityId, businessPartnerId);
    filterSelect(projectSelect, legalEntityId, businessPartnerId);
    const selectedProject = projectSelect?.selectedOptions[0];
    const forcedCrewId = requiresCrewOwner && selectedOwner && selectedOwner.dataset.role === 'crew'
      ? businessPartnerId
      : '';
    filterCrewSelect(crewSelect, selectedProject, forcedCrewId);
  };

  ownerSelect.addEventListener('change', () => synchronize({ clearDependents: true }));
  if (scopeSelect) scopeSelect.addEventListener('change', () => synchronize({ clearDependents: true }));
  if (externalTypeSelect) externalTypeSelect.addEventListener('change', () => synchronize({ clearDependents: true }));
  if (projectSelect) projectSelect.addEventListener('change', () => {
    if (crewSelect && !crewSelect.dataset.forcedByOwner) crewSelect.value = '';
    synchronize();
  });
  synchronize();
};

document.querySelectorAll('[data-personnel-affiliation]').forEach(initializeAffiliationEditor);

export { filterCrewSelect, filterOwnerSelect, filterSelect, initializeAffiliationEditor, optionMatchesOwner, synchronizeTypeSelect };
