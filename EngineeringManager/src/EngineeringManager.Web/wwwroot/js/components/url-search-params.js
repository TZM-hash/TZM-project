export function deleteSearchParamsIgnoreCase(searchParams, names) {
  const requested = new Set(names.map((name) => name.toLowerCase()));
  Array.from(searchParams.keys()).forEach((key) => {
    if (requested.has(key.toLowerCase())) searchParams.delete(key);
  });
}
