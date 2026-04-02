local Class = {}

function Class.extend(base)
    local derived = {}
    derived.__index = derived
    setmetatable(derived, base)
    return derived
end

return Class
