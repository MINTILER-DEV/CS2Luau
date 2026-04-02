local Collections = {}

function Collections.clone(list)
    local copy = {}
    for index, value in ipairs(list) do
        copy[index] = value
    end
    return copy
end

return Collections
