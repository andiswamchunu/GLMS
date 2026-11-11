// Live exchange-rate lookup and auto-calculation of the ZAR cost on the "New service request" page.
// The server recalculates the cost on save; this script is only a preview.
(function () {
    'use strict';

    var form = document.getElementById('sr-form');
    if (!form) { return; }

    var amountInput = document.getElementById('Amount');
    var currencySelect = document.getElementById('CurrencyCode');
    var contractSelect = document.getElementById('ContractId');
    var totalEl = document.getElementById('zar-total');
    var rateLine = document.getElementById('rate-line');
    var rateWarning = document.getElementById('rate-warning');
    var contractWarning = document.getElementById('contract-warning');
    var currentRate = null;
    var requestToken = 0;

    function formatZar(value) {
        return 'R ' + value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    // Same maths as CurrencyConverter.ConvertToZar: amount x rate rounded to 2 decimals.
    function recalc() {
        var amount = parseFloat(amountInput.value);
        if (currentRate && isFinite(amount) && amount > 0) {
            totalEl.textContent = formatZar(Math.round((amount * currentRate + Number.EPSILON) * 100) / 100);
        } else {
            totalEl.textContent = 'R 0.00';
        }
    }

    function loadRate() {
        var token = ++requestToken;
        var code = currencySelect.value;
        currentRate = null;
        rateLine.textContent = 'Loading today\'s ' + code + ' to ZAR rate…';
        rateWarning.classList.add('d-none');
        recalc();

        fetch(form.dataset.rateUrl + '?currency=' + encodeURIComponent(code), { headers: { 'Accept': 'application/json' } })
            .then(function (response) {
                return response.json().then(function (body) { return { ok: response.ok, body: body }; });
            })
            .then(function (result) {
                if (token !== requestToken) { return; } // a newer request has replaced this one
                if (!result.ok) { throw new Error(result.body && result.body.error ? result.body.error : 'Rate unavailable.'); }

                currentRate = result.body.rate;
                rateLine.textContent = '1 ' + result.body.currency + ' = R ' + currentRate.toFixed(4) + ' · ' + result.body.source;
                if (result.body.isFallback) {
                    rateWarning.textContent = 'The live rate service could not be reached, so a stored rate is being used.';
                    rateWarning.classList.remove('d-none');
                }
                recalc();
            })
            .catch(function (err) {
                if (token !== requestToken) { return; }
                rateLine.textContent = 'Exchange rate unavailable.';
                rateWarning.textContent = err.message + ' You can still submit; the server will try again.';
                rateWarning.classList.remove('d-none');
            });
    }

    function checkContract() {
        var option = contractSelect.options[contractSelect.selectedIndex];
        if (option && option.dataset.eligible === 'false') {
            contractWarning.textContent = option.dataset.reason || 'Service requests cannot be created for this contract.';
            contractWarning.classList.remove('d-none');
        } else {
            contractWarning.classList.add('d-none');
        }
    }

    amountInput.addEventListener('input', recalc);
    currencySelect.addEventListener('change', loadRate);
    contractSelect.addEventListener('change', checkContract);

    checkContract();
    loadRate();
})();
