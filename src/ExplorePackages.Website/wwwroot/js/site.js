
(function () {
    // Inspired by: https://scottdorman.blog/2018/08/18/saving-bootstrap-component-state/
    const collapseStateKey = "collapse-state";

    function restoreCollapseState() {
        var state = localStorage.getItem(collapseStateKey);
        if (state) {
            state = JSON.parse(state);
            for (const id in state) {
                var el = document.getElementById(id);
                if (!el) {
                    continue;
                }

                var labelledBy = el.getAttribute('aria-labelledBy');
                var labelledByEl = document.getElementById(labelledBy);

                var hide = state[id];
                if (hide) {
                    el.classList.remove('show');
                    labelledByEl.classList.add('collapsed');
                } else {
                    el.classList.add('show');
                    labelledByEl.classList.remove('collapsed');
                }
            }
        }
    }

    function setCollapseState(id, hide) {
        var state = localStorage.getItem(collapseStateKey);
        if (!state) {
            state = {};
        } else {
            state = JSON.parse(state);
        }

        state[id] = hide;
        localStorage.setItem(collapseStateKey, JSON.stringify(state));
    }

    $(function () {
        $('input[name="useCustomMax"]').on('click', function () {
            $(this).parent().siblings('.custom-max').toggle();
        });

        $('.collapse-remember').on('hide.bs.collapse show.bs.collapse', function (e) {
            $(this).data('kc.hidden', e.type == 'hide');
            setCollapseState(e.target.id, e.type == 'hide');
        });

        $('.card-header[role="button"]').dblclick(function () {
            var hidden = $(this).parent().find('.collapse-remember').data('kc.hidden');
            $('.collapse-remember').collapse(hidden ? 'hide' : 'show');
        });
    });

    restoreCollapseState();
})();
