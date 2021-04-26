
(function () {
    // Inspired by: https://scottdorman.blog/2018/08/18/saving-bootstrap-component-state/
    const collapseStateKey = "collapse-state";

    function getFragmentKey() {
        if (window.location.hash.length > 0) {
            return window.location.hash.substr(1);
        } else {
            return "";
        }
    }

    function restoreCollapseState() {
        var state = localStorage.getItem(collapseStateKey);

        // Check the hash (fragment) and expand the section if the hash matches the section.
        var fragmentKey = getFragmentKey();

        if (state) {
            state = JSON.parse(state);
            for (const id in state) {
                var el = document.getElementById(id);
                if (!el) {
                    continue;
                }

                var labelledBy = el.getAttribute('aria-labelledBy');
                var labelledByEl = document.getElementById(labelledBy);

                var hide = state[id] && fragmentKey != getKeyFromContentId(id);
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

    function getKeyFromContentId(id) {
        return id.substr(0, id.length - "-content".length);
    }

    $(function () {
        $('input[name="useCustomMax"]').on('click', function () {
            $(this).parent().siblings('.custom-max').toggle();
        });

        $('.collapse-remember').on('hide.bs.collapse show.bs.collapse', function (e) {
            $(this).data('kc.hidden', e.type == 'hide');
            setCollapseState(this.id, e.type == 'hide');
        });

        $('.collapse-remember').on('hide.bs.collapse', function (e) {
            if (getKeyFromContentId(this.id) == getFragmentKey()) {
                history.replaceState("", document.title, window.location.pathname + window.location.search);
            }
        });

        $('.card [data-toggle="collapse"]').dblclick(function () {
            var hidden = $(this).parents(".card").find('.collapse-remember').data('kc.hidden');
            $('.collapse-remember').collapse(hidden ? 'hide' : 'show');
        });
    });

    restoreCollapseState();
})();
