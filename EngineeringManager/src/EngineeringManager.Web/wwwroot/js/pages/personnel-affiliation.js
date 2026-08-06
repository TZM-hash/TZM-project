const splitIds = (value) => (value || '').split(',').map((item) => item.trim()).filter(Boolean);

const optionMatchesOwner = (option, legalEntityId, businessPartnerId) => {
  if (!option.value) return true;
  const legalEntityIds = splitIds(option.dataset.legalEntityId);
  const businessPartnerIds = splitIds(option.dataset.businessPartnerId);
  if (legalEntityId) return legalEntityIds.includes(legalEntityId);
  if (businessPartnerId) return businessPartnerIds.includes(businessPartnerId);
  return false;
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

const initializeAffiliationEditor = (root) => {
  const ownerSelect = root.querySelector('[data-affiliation-owner]');
  const departmentSelect = root.querySelector('[data-affiliation-department]');
  const projectSelect = root.querySelector('[data-affiliation-project]');
  const crewSelect = root.querySelector('[data-affiliation-crew]');
  const legalEntityInput = root.querySelector('[data-affiliation-legal-entity]');
  const businessPartnerInput = root.querySelector('[data-affiliation-business-partner]');
  if (!ownerSelect || !legalEntityInput || !businessPartnerInput) return;

  const synchronize = ({ clearDependents = false } = {}) => {
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

    if (selectedOwner && selectedOwner.dataset.role === 'crew' && crewSelect) {
      crewSelect.value = businessPartnerId;
      crewSelect.dataset.forcedByOwner = 'true';
    } else if (crewSelect) {
      delete crewSelect.dataset.forcedByOwner;
    }
  };

  ownerSelect.addEventListener('change', () => synchronize({ clearDependents: true }));
  synchronize();
};

document.querySelectorAll('[data-personnel-affiliation]').forEach(initializeAffiliationEditor);

export { filterSelect, initializeAffiliationEditor, optionMatchesOwner };
