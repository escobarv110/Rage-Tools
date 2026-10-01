Config = Config or { host = "127.0.0.1", port = 27018 }

local connected = false
local live = {}
local cam = nil
local camActive = false
local reportPlayer = false

local function say(text)
    print(("[rage_tools] %s"):format(text))
    TriggerEvent('chat:addMessage', { args = { 'RAGE Tools', text } })
end

local function send(tbl)
    if not connected then return end
    SendNUIMessage({ send = json.encode(tbl) })
end

local function num(v, dflt)
    local n = tonumber(v)
    if n == nil then n = dflt or 0 end
    return n + 0.0
end

local function vec(arr, dflt)
    if type(arr) ~= 'table' then return dflt or vector3(0.0, 0.0, 0.0) end
    return vector3(num(arr[1]), num(arr[2]), num(arr[3]))
end

local function pushConfig()
    SendNUIMessage({ connect = { host = Config.host, port = Config.port } })
end

local function rotDir(rot)
    local p = math.rad(num(rot.x))
    local y = math.rad(num(rot.z))
    return vector3(-math.sin(y) * math.cos(p), math.cos(y) * math.cos(p), math.sin(p))
end

local function quatRotate(q, v)
    local qx, qy, qz, qw = q[1], q[2], q[3], q[4]
    local tx = 2.0 * (qy * v.z - qz * v.y)
    local ty = 2.0 * (qz * v.x - qx * v.z)
    local tz = 2.0 * (qx * v.y - qy * v.x)
    return vector3(v.x + qw * tx + (qy * tz - qz * ty), v.y + qw * ty + (qz * tx - qx * tz), v.z + qw * tz + (qx * ty - qy * tx))
end

local function hourOn(flags, hour)
    local f = math.tointeger(flags) or 0xFFFFFF
    if f == 0 then return true end
    return ((f >> hour) & 1) == 1
end

local function loadModel(hash)
    if not IsModelInCdimage(hash) then return false end
    RequestModel(hash)
    local tries = 0
    while not HasModelLoaded(hash) and tries < 40 do
        Wait(50)
        tries = tries + 1
    end
    return HasModelLoaded(hash)
end

local function releaseSet(set)
    if set.entity and DoesEntityExist(set.entity) then SetEntityLights(set.entity, false) end
    if set.spawned and DoesEntityExist(set.spawned) then DeleteObject(set.spawned) end
    if set.hidden and set.place then
        local p = set.place.pos
        RemoveModelHide(p.x, p.y, p.z, 1.5, set.hash, false)
        set.hidden = nil
    end
    set.entity = nil
    set.spawned = nil
    set.worldOnly = nil
end

local function clearLights()
    for _, set in pairs(live) do releaseSet(set) end
    live = {}
end

local ents = {}

local function distance(a, b)
    local d = a - b
    return math.sqrt(d.x * d.x + d.y * d.y + d.z * d.z)
end

local saidInterior = {}

local function inInterior(p)
    return GetInteriorAtCoords(p.x, p.y, p.z) ~= 0
end

local function standInAt(hash, pos)
    for _, e in pairs(ents) do
        if e.hash == hash and e.spawned and DoesEntityExist(e.spawned) and e.place and distance(e.place.pos, pos) < 0.05 then
            return e.spawned
        end
    end
    return 0
end

local function releaseEntity(e)
    if e.spawned and DoesEntityExist(e.spawned) then DeleteObject(e.spawned) end
    e.spawned = nil
    if e.hidden and e.orig then
        RemoveModelHide(e.orig.pos.x, e.orig.pos.y, e.orig.pos.z, 1.5, e.hash, false)
        e.hidden = nil
    end
end

local function clearEntities()
    for _, e in pairs(ents) do releaseEntity(e) end
    ents = {}
end

local function queueEntity(data)
    if data.clear then
        clearEntities()
        return
    end
    local id = tostring(data.id or '')
    if id == '' then return end
    local e = ents[id]
    if not e then
        e = { hash = math.tointeger(data.hash) or GetHashKey(tostring(data.model or '')), model = tostring(data.model or '') }
        ents[id] = e
    end
    e.pending = data
end

local function applyEntity(e, data)
    if type(data.orig) == 'table' then e.orig = { pos = vec(data.orig.pos) } else e.orig = nil end
    local place = data.place
    if type(place) ~= 'table' then
        if e.spawned and DoesEntityExist(e.spawned) then DeleteObject(e.spawned) end
        e.spawned = nil
        e.place = nil
        if e.orig and not e.hidden then
            CreateModelHide(e.orig.pos.x, e.orig.pos.y, e.orig.pos.z, 1.5, e.hash, true)
            e.hidden = true
        end
        return
    end
    local r = place.rot or {}
    e.place = { pos = vec(place.pos), rot = { num(r[1]), num(r[2]), num(r[3]), num(r[4], 1.0) } }
    if not data.moved then
        releaseEntity(e)
        return
    end
    if not (e.spawned and DoesEntityExist(e.spawned)) and e.orig and inInterior(e.orig.pos) then
        if not e.warnedInterior then
            e.warnedInterior = true
            say(('%s is part of an interior - the game cannot hide it, so save and restart the resource to see the move'):format(e.model))
        end
        return
    end
    if not (e.spawned and DoesEntityExist(e.spawned)) then
        e.spawned = nil
        if loadModel(e.hash) then
            local p = e.place.pos
            local obj = CreateObjectNoOffset(e.hash, p.x, p.y, p.z, false, false, false)
            if obj ~= 0 then
                FreezeEntityPosition(obj, true)
                e.spawned = obj
            end
        end
        if not e.spawned and not e.warned then
            e.warned = true
            say(('%s: the game will not spawn a copy of this model, leaving the original where it is'):format(e.model))
        end
    end
    if e.spawned then
        local p, q = e.place.pos, e.place.rot
        SetEntityCoordsNoOffset(e.spawned, p.x, p.y, p.z, false, false, false)
        SetEntityQuaternion(e.spawned, q[1], q[2], q[3], q[4])
        if e.orig and not e.hidden then
            CreateModelHide(e.orig.pos.x, e.orig.pos.y, e.orig.pos.z, 1.5, e.hash, true)
            e.hidden = true
        end
    end
end

local function findEntity(set)
    if set.place then
        local p = set.place.pos
        local ent = standInAt(set.hash, p)
        if ent == 0 then ent = GetClosestObjectOfType(p.x, p.y, p.z, 2.0, set.hash, false, false, false) end
        if ent == 0 and not set.spawned and not set.worldOnly and inInterior(p) then
            set.worldOnly = true
            if not saidInterior[set.model] then
                saidInterior[set.model] = true
                say(('%s: inside an interior, drawing the lights at their place without a copy'):format(set.model))
            end
        end
        if ent == 0 and not set.spawned and not set.worldOnly then
            if loadModel(set.hash) then
                ent = CreateObjectNoOffset(set.hash, p.x, p.y, p.z, false, false, false)
                if ent ~= 0 then
                    local q = set.place.rot
                    SetEntityQuaternion(ent, q[1], q[2], q[3], q[4])
                    FreezeEntityPosition(ent, true)
                    set.spawned = ent
                    if not set.hidden then
                        CreateModelHide(p.x, p.y, p.z, 1.5, set.hash, true)
                        set.hidden = true
                    end
                    say(('%s: showing your lights on a stand-in copy at its place'):format(set.model))
                end
            end
            if ent == 0 then
                set.worldOnly = true
                say(('%s: no model to stand in, drawing the lights at their world position'):format(set.model))
            end
        end
        if ent ~= 0 then
            set.entity = ent
            SetEntityLights(ent, true)
        end
        return
    end
    local ped = PlayerPedId()
    local p = GetEntityCoords(ped)
    local ent = GetClosestObjectOfType(p.x, p.y, p.z, 250.0, set.hash, false, false, false)
    if ent == 0 and set.spawn and not set.spawned then
        if loadModel(set.hash) then
            local at = p + GetEntityForwardVector(ped) * 3.0
            ent = CreateObjectNoOffset(set.hash, at.x, at.y, at.z, false, false, false)
            if ent ~= 0 then
                FreezeEntityPosition(ent, true)
                SetEntityHeading(ent, GetEntityHeading(ped))
                set.spawned = ent
                say(('%s is not near you - placed a preview copy in front of you'):format(set.model))
            end
        else
            say(('no model called %s is streamed on this server - save it into a resource first'):format(set.model))
            set.spawn = false
        end
    end
    if ent ~= 0 then
        set.entity = ent
        SetEntityLights(ent, true)
    end
end

local function applyLights(data)
    local keep = {}
    for _, prop in ipairs(data.props or {}) do
        local model = tostring(prop.model or '')
        local key = tostring(prop.key or model)
        if model ~= '' then
            keep[key] = true
            local set = live[key]
            if not set then
                set = { model = model, hash = GetHashKey(model), lights = {}, searched = 0 }
                live[key] = set
            end
            local ls = {}
            for _, l in ipairs(prop.lights or {}) do
                ls[#ls + 1] = {
                    i = math.tointeger(l.i) or 0,
                    kind = tostring(l.kind or 'point'),
                    pos = vec(l.pos),
                    dir = vec(l.dir, vector3(0.0, 0.0, -1.0)),
                    tan = vec(l.tan, vector3(1.0, 0.0, 0.0)),
                    r = math.floor(num(l.rgb and l.rgb[1], 255)),
                    g = math.floor(num(l.rgb and l.rgb[2], 255)),
                    b = math.floor(num(l.rgb and l.rgb[3], 255)),
                    intensity = num(l.intensity, 1.0),
                    range = num(l.range, 5.0),
                    exp = num(l.exp, 1.0),
                    inner = num(l.inner, 10.0),
                    outer = num(l.outer, 35.0),
                    extent = num(l.extent, 0.0),
                    time = l.time,
                    shadow = l.shadow == true,
                    flash = math.tointeger(l.flash) or 0,
                }
            end
            set.lights = ls
            set.spawn = data.spawn ~= false
            if type(prop.place) == 'table' then
                local r = prop.place.rot or {}
                set.place = { pos = vec(prop.place.pos), rot = { num(r[1]), num(r[2]), num(r[3]), num(r[4], 1.0) }, scale = vec(prop.place.scale, vector3(1.0, 1.0, 1.0)) }
                if set.spawned and DoesEntityExist(set.spawned) then
                    local p = set.place.pos
                    SetEntityCoordsNoOffset(set.spawned, p.x, p.y, p.z, false, false, false)
                    SetEntityQuaternion(set.spawned, set.place.rot[1], set.place.rot[2], set.place.rot[3], set.place.rot[4])
                end
            else
                set.place = nil
            end
        end
    end
    for key, set in pairs(live) do
        if not keep[key] then
            releaseSet(set)
            live[key] = nil
        end
    end
end

local function hash01(x)
    local s = math.sin(x * 127.1) * 43758.5453
    return s - math.floor(s)
end

local function noise1(t)
    local f = math.floor(t)
    local fr = t - f
    local a = hash01(f)
    local b = hash01(f + 1)
    local u = fr * fr * (3 - 2 * fr)
    return a + (b - a) * u
end

local function flashMul(flash, time, seed)
    local s = (seed * 0.6180339887) % 1.0
    if flash == 0 or flash == 10 or flash == 18 then return 1.0 end
    if flash == 1 or flash == 2 or flash == 6 then
        if hash01(math.floor(time * 9.0) + s * 100.0) > 0.5 then return 1.0 else return 0.35 end
    end
    if flash == 3 then if ((time + s) % 1.0) < 0.5 then return 1.0 else return 0.0 end end
    if flash == 4 then if ((time + s) % 0.5) < 0.25 then return 1.0 else return 0.0 end end
    if flash == 5 then if ((time + s) % 0.2) < 0.1 then return 1.0 else return 0.0 end end
    if flash == 7 then return 0.0 end
    if flash == 9 then return 0.5 + 0.5 * math.sin((time + s) * math.pi * 2.0 / 1.5) end
    if flash == 11 then if ((time / 3.0) % 1.0) < 0.333 then return 1.0 else return 0.0 end end
    if flash == 12 then if ((time / 3.0 + 0.333) % 1.0) < 0.333 then return 1.0 else return 0.0 end end
    if flash == 13 then if ((time / 3.0 + 0.666) % 1.0) < 0.333 then return 1.0 else return 0.0 end end
    if flash == 14 then if hash01(math.floor(time * 4.0) + s) > 0.4 then return 1.0 else return 0.1 end end
    if flash == 15 then return 0.75 + 0.25 * noise1(time * 3.0 + s * 10.0) end
    if flash == 16 then if ((time + s) % 1.2) < 0.1 then return 1.0 else return 0.05 end end
    if flash == 17 then return 0.65 + 0.35 * noise1(time * 6.0 + s * 10.0) end
    if flash == 19 then if hash01(math.floor(time * 14.0) + s) > 0.12 then return 1.0 else return 0.2 end end
    if flash == 20 then if ((time + s) % 0.15) < 0.05 then return 1.0 else return 0.0 end end
    return 1.0
end

local function drawLightWorld(l, wp, wd)
    local intensity = l.intensity * (l.mul or 1.0)
    local shadowId = 700 + l.i
    if l.kind == 'spot' then
        local hardness = 1.0
        if l.outer > 0.01 then hardness = math.min(math.max(l.inner / l.outer, 0.0), 1.0) end
        if l.shadow then
            DrawSpotLightWithShadow(wp.x, wp.y, wp.z, wd.x, wd.y, wd.z, l.r, l.g, l.b, l.range, intensity, hardness, l.outer, l.exp, shadowId)
        else
            DrawSpotLight(wp.x, wp.y, wp.z, wd.x, wd.y, wd.z, l.r, l.g, l.b, l.range, intensity, hardness, l.outer, l.exp)
        end
    elseif l.kind == 'capsule' and l.extent > 0.05 then
        local half = l.extent * 0.5
        local step = wd * half
        local each = intensity * 0.5
        DrawLightWithRange(wp.x - step.x, wp.y - step.y, wp.z - step.z, l.r, l.g, l.b, l.range, each)
        DrawLightWithRange(wp.x, wp.y, wp.z, l.r, l.g, l.b, l.range, each)
        DrawLightWithRange(wp.x + step.x, wp.y + step.y, wp.z + step.z, l.r, l.g, l.b, l.range, each)
    else
        if l.shadow then
            DrawLightWithRangeAndShadow(wp.x, wp.y, wp.z, l.r, l.g, l.b, l.range, intensity, 0.0)
        else
            DrawLightWithRange(wp.x, wp.y, wp.z, l.r, l.g, l.b, l.range, intensity)
        end
    end
end

local function drawLight(l, ent)
    local origin = GetOffsetFromEntityInWorldCoords(ent, 0.0, 0.0, 0.0)
    local wp = GetOffsetFromEntityInWorldCoords(ent, l.pos.x, l.pos.y, l.pos.z)
    local wd = GetOffsetFromEntityInWorldCoords(ent, l.dir.x, l.dir.y, l.dir.z) - origin
    drawLightWorld(l, wp, wd)
end

local function drawLightAt(l, place)
    local s = place.scale
    local lp = vector3(l.pos.x * s.x, l.pos.y * s.y, l.pos.z * s.z)
    local wp = place.pos + quatRotate(place.rot, lp)
    local wd = quatRotate(place.rot, l.dir)
    drawLightWorld(l, wp, wd)
end

local function startCam(pos, target, fov)
    if not cam then cam = CreateCam('DEFAULT_SCRIPTED_CAMERA', true) end
    SetCamCoord(cam, pos.x, pos.y, pos.z)
    local d = target - pos
    local len = math.sqrt(d.x * d.x + d.y * d.y + d.z * d.z)
    if len > 0.0001 then
        d = d / len
        local pitch = math.deg(math.asin(math.min(math.max(d.z, -1.0), 1.0)))
        local yaw = math.deg(math.atan(-d.x, d.y))
        SetCamRot(cam, pitch, 0.0, yaw, 2)
    end
    SetCamFov(cam, math.min(math.max(fov, 10.0), 130.0))
    if not camActive then
        SetCamActive(cam, true)
        RenderScriptCams(true, false, 0, true, true)
        camActive = true
    end
    SetFocusPosAndVel(pos.x, pos.y, pos.z, 0.0, 0.0, 0.0)
end

local function stopCam()
    if camActive then
        RenderScriptCams(false, false, 0, true, true)
        camActive = false
    end
    if cam then
        SetCamActive(cam, false)
        DestroyCam(cam, false)
        cam = nil
    end
    ClearFocus()
end

local lockedWeather = nil
local lockedHour = nil
local lockedMinute = 0

local function applyWeather(name)
    ClearOverrideWeather()
    ClearWeatherTypePersist()
    SetWeatherTypePersist(name)
    SetWeatherTypeNow(name)
    SetWeatherTypeNowPersist(name)
    SetOverrideWeather(name)
end

local function setWeather(name)
    lockedWeather = tostring(name or 'CLEAR'):upper()
    applyWeather(lockedWeather)
end

local function setTime(hour, minute)
    lockedHour = hour
    lockedMinute = minute
    NetworkOverrideClockTime(hour, minute, 0)
end

local function clearLocks()
    if lockedWeather then
        ClearOverrideWeather()
        ClearWeatherTypePersist()
        lockedWeather = nil
    end
    if lockedHour then
        NetworkClearClockTimeOverride()
        lockedHour = nil
    end
end

local liveTc = {}
local liveTcOn = false
local extraMod = nil

local function ensureModifier(name)
    if GetTimecycleModifierIndexByName(name) == -1 then CreateTimecycleModifier(name) end
end

local function applyTimecycle(data)
    if data.off then
        if liveTcOn then
            ClearTimecycleModifier()
            liveTcOn = false
        end
        return
    end
    ensureModifier('ragetools_live')
    local seen = {}
    for name, value in pairs(data.vars or {}) do
        local v = num(value)
        SetTimecycleModifierVar('ragetools_live', name, v, v)
        seen[name] = true
        liveTc[name] = true
    end
    for name in pairs(liveTc) do
        if not seen[name] then
            RemoveTimecycleModifierVar('ragetools_live', name)
            liveTc[name] = nil
        end
    end
    if not liveTcOn then
        SetTimecycleModifier('ragetools_live')
        liveTcOn = true
    end
    SetTimecycleModifierStrength(1.0)
end

local function applyTcMods(data)
    if data.off then
        if extraMod then
            ClearExtraTimecycleModifier()
            extraMod = nil
        end
        return
    end
    for _, mod in ipairs(data.mods or {}) do
        local name = tostring(mod.name or '')
        if name ~= '' then
            ensureModifier(name)
            for var, value in pairs(mod.vars or {}) do
                local v = num(value)
                SetTimecycleModifierVar(name, var, v, v)
            end
        end
    end
    local apply = data.apply
    if type(apply) == 'string' and apply ~= '' then
        if extraMod ~= apply then
            SetExtraTimecycleModifier(apply)
            extraMod = apply
        end
        SetExtraTimecycleModifierStrength(num(data.strength, 1.0))
    elseif extraMod then
        ClearExtraTimecycleModifier()
        extraMod = nil
    end
end

local function reportStats()
    local ped = PlayerPedId()
    local p = GetEntityCoords(ped)
    local n = 0
    for _, set in pairs(live) do
        if set.entity then n = n + #set.lights end
    end
    send({
        type = 'stats',
        fps = math.floor(1.0 / math.max(GetFrameTime(), 0.001) + 0.5),
        ping = GetPlayerPing(PlayerId()),
        pos = { p.x, p.y, p.z },
        heading = GetEntityHeading(ped),
        interior = GetInteriorFromEntity(ped),
        room = GetRoomKeyFromEntity(ped),
        lights = n,
        cam = camActive,
        tc = liveTcOn,
        mod = extraMod or '',
    })
end

local lastHeard = 0

local function restoreAll()
    clearLights()
    stopCam()
    applyTimecycle({ off = true })
    applyTcMods({ off = true })
    clearEntities()
    clearLocks()
    reportPlayer = false
    SetPlayerControl(PlayerId(), true, 0)
end

local function reportWhere()
    local ped = PlayerPedId()
    local p = GetEntityCoords(ped)
    local cp = GetGameplayCamCoord()
    local cr = GetGameplayCamRot(2)
    send({
        type = 'player',
        pos = { p.x, p.y, p.z },
        heading = GetEntityHeading(ped),
        campos = { cp.x, cp.y, cp.z },
        camrot = { cr.x, cr.y, cr.z },
        fov = GetGameplayCamFov(),
    })
end

local function handle(data)
    local t = data.type
    if t == 'hello' then
        say(('linked to RAGE Tools %s'):format(tostring(data.version or '')))
    elseif t == 'lights' then
        applyLights(data)
    elseif t == 'camera' then
        if data.off then stopCam() else startCam(vec(data.pos), vec(data.target), num(data.fov, 50.0)) end
    elseif t == 'follow' then
        reportPlayer = data.on == true
    elseif t == 'time' then
        setTime(math.tointeger(data.hour) or 12, math.tointeger(data.minute) or 0)
    elseif t == 'weather' then
        setWeather(data.name)
    elseif t == 'restart' then
        TriggerServerEvent('ragetools:restart', tostring(data.resource or ''))
    elseif t == 'goto' then
        local p = vec(data.pos)
        SetEntityCoords(PlayerPedId(), p.x, p.y, p.z, false, false, false, false)
    elseif t == 'where' then
        reportWhere()
    elseif t == 'entity' then
        queueEntity(data)
    elseif t == 'timecycle' then
        applyTimecycle(data)
    elseif t == 'tcmod' then
        applyTcMods(data)
    elseif t == 'ping' then
        send({ type = 'pong' })
    elseif t == 'bye' then
        say('RAGE Tools closed - putting the game back')
        restoreAll()
        SendNUIMessage({ reconnect = true })
    end
end

RegisterNUICallback('ready', function(_, cb)
    pushConfig()
    cb('ok')
end)

RegisterNUICallback('ws', function(data, cb)
    local was = connected
    connected = data.connected == true
    if connected and not was then
        say('linked to RAGE Tools')
        lastHeard = GetGameTimer()
        send({ type = 'hello', app = 'FiveM', player = GetPlayerName(PlayerId()), resource = GetCurrentResourceName(), version = Config.version or 0 })
    elseif was and not connected then
        say('RAGE Tools link lost - putting the game back, retrying')
        restoreAll()
    end
    cb('ok')
end)

RegisterNUICallback('msg', function(data, cb)
    lastHeard = GetGameTimer()
    if type(data) == 'table' then handle(data) end
    cb('ok')
end)

RegisterNetEvent('ragetools:say', function(text)
    say(tostring(text))
    send({ type = 'say', text = tostring(text) })
end)

RegisterCommand('ragetools_pick', function()
    local cp = GetGameplayCamCoord()
    local d = rotDir(GetGameplayCamRot(2))
    local to = cp + d * 200.0
    local ray = StartShapeTestRay(cp.x, cp.y, cp.z, to.x, to.y, to.z, -1, PlayerPedId(), 0)
    local _, hit, at, _, ent = GetShapeTestResult(ray)
    if hit and ent ~= 0 then
        local name = GetEntityArchetypeName(ent) or ''
        local ep = GetEntityCoords(ent)
        local er = GetEntityRotation(ent, 2)
        send({
            type = 'pick',
            name = name,
            hash = GetEntityModel(ent),
            pos = { ep.x, ep.y, ep.z },
            rot = { er.x, er.y, er.z },
            hit = { at.x, at.y, at.z },
            interior = GetInteriorFromEntity(ent),
        })
        say(('picked %s'):format(name ~= '' and name or tostring(GetEntityModel(ent))))
    else
        say('nothing picked - look at a prop and press the key again')
    end
end, false)
RegisterKeyMapping('ragetools_pick', 'RAGE Tools: pick the prop you look at', 'keyboard', 'F9')

CreateThread(function()
    Wait(1000)
    pushConfig()
end)

CreateThread(function()
    while true do
        local sleep = 500
        if next(live) ~= nil then
            sleep = 0
            local hour = GetClockHours()
            local now = GetGameTimer()
            for _, set in pairs(live) do
                if set.entity and not DoesEntityExist(set.entity) then set.entity = nil end
                if not set.entity and now - set.searched > 1000 then
                    set.searched = now
                    findEntity(set)
                end
                local ent = set.entity
                local seedBase = (set.hash & 0xFFFFFFFF) % 9973
                local t = now / 1000.0
                if ent then
                    SetEntityLights(ent, true)
                    for _, l in ipairs(set.lights) do
                        if hourOn(l.time, hour) then
                            l.mul = flashMul(l.flash or 0, t, seedBase + l.i * 977)
                            if l.mul > 0.001 then drawLight(l, ent) end
                        end
                    end
                elseif set.place then
                    for _, l in ipairs(set.lights) do
                        if hourOn(l.time, hour) then
                            l.mul = flashMul(l.flash or 0, t, seedBase + l.i * 977)
                            if l.mul > 0.001 then drawLightAt(l, set.place) end
                        end
                    end
                end
            end
        end
        Wait(sleep)
    end
end)

CreateThread(function()
    while true do
        if connected and reportPlayer then
            reportWhere()
            Wait(100)
        else
            Wait(500)
        end
    end
end)

CreateThread(function()
    while true do
        for _, e in pairs(ents) do
            local data = e.pending
            if data then
                e.pending = nil
                applyEntity(e, data)
            end
        end
        Wait(0)
    end
end)

CreateThread(function()
    while true do
        if connected then
            reportStats()
            Wait(500)
        else
            Wait(1000)
        end
    end
end)

CreateThread(function()
    while true do
        if connected and lastHeard > 0 and GetGameTimer() - lastHeard > 8000 then
            lastHeard = 0
            say('RAGE Tools stopped answering - putting the game back')
            restoreAll()
            SendNUIMessage({ reconnect = true })
        end
        if connected and lockedWeather then applyWeather(lockedWeather) end
        if connected and lockedHour then NetworkOverrideClockTime(lockedHour, lockedMinute, 0) end
        Wait(500)
    end
end)

AddEventHandler('onResourceStop', function(name)
    if name ~= GetCurrentResourceName() then return end
    restoreAll()
end)
